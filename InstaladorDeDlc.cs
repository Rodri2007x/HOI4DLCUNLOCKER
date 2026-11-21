using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace DlcDeHoi4;

internal sealed record ProgresoDeDescarga(string Mensaje, long Descargados, long Total);

internal static class InstaladorDeDlc
{
    public static async Task InstalarAsync(string carpeta, CatalogoDeDlc catalogo, uint[] ids,
        IProgress<ProgresoDeDescarga>? progreso, CancellationToken cancelacion, bool incluirSoundtracks = false)
    {
        cancelacion.ThrowIfCancellationRequested();
        string raiz = DetectorDeDlc.VerificarInstalacion(carpeta).FullName;
        string destinoDlc = Path.Combine(raiz, "dlc");
        string rutaMetadatos = InstalacionDeHoi4.RutaDeMetadatos(raiz);
        ValidarRutasDeJuego(raiz);

        using var bloqueo = new FileStream(Path.Combine(raiz, ".dlcdehoi4-instalar.lock"), FileMode.CreateNew,
            FileAccess.Write, FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose);
        InstalacionDeHoi4 instalacion = InstalacionDeHoi4.Leer(raiz, cancelacion);
        if (instalacion.Metadatos is null)
        {
            throw new InvalidDataException("Falta un 00_dlc_info.txt válido en la instalación de HOI4.");
        }

        var seleccion = new List<(FichaDeDlc Ficha, PaqueteDeDlc Paquete, bool InstalarDlc, SoundtrackDeDlc? Soundtrack)>();
        foreach (uint id in ids.Distinct())
        {
            FichaDeDlc? ficha = catalogo.Dlcs.FirstOrDefault(candidata => candidata.AppId == id);
            if (ficha?.Paquete is not PaqueteDeDlc paquete)
            {
                throw new InvalidDataException($"El DLC {id} carece de un paquete en el catálogo.");
            }

            bool instalarDlc = !instalacion.EstaInstalado(ficha);
            SoundtrackDeDlc? soundtrack = incluirSoundtracks && !instalacion.SoundtrackInstalado(paquete) ? paquete.Soundtrack : null;
            if (!instalarDlc && soundtrack is null)
            {
                continue;
            }

            if (instalacion.BuildId != paquete.BuildId)
            {
                throw new InvalidDataException($"{ficha.Nombre} requiere la build {paquete.BuildId} de HOI4.");
            }

            if (instalarDlc)
            {
                ValidarDestino(raiz, Path.Combine(destinoDlc, paquete.Carpeta), ficha.AppId, cancelacion);
            }

            if (soundtrack is not null)
            {
                ValidarDestinoSoundtrack(InstalacionDeHoi4.RutaDeSoundtrack(raiz, paquete.Carpeta), soundtrack.Pistas);
            }

            seleccion.Add((ficha, paquete, instalarDlc, soundtrack));
        }

        if (seleccion.Count == 0)
        {
            return;
        }

        long total = seleccion.Sum(entrada =>
            checked((entrada.InstalarDlc ? entrada.Paquete.Bytes : 0) + (entrada.Soundtrack?.Bytes ?? 0)));
        string temporal = Directory.CreateDirectory(Path.Combine(raiz, $".dlcdehoi4-{Guid.NewGuid():N}")).FullName;
        bool conservarRespaldo = false;
        try
        {
            var preparados = new List<DlcPreparado>();
            var soundtracksPreparados = new List<SoundtrackPreparado>();
            long anteriores = 0;
            foreach (var (ficha, paquete, instalarDlc, soundtrack) in seleccion)
            {
                string zona = Directory.CreateDirectory(Path.Combine(temporal, ficha.AppId.ToString(CultureInfo.InvariantCulture))).FullName;
                if (instalarDlc)
                {
                    string archivo = Path.Combine(zona, "descarga.zip");
                    await DescargarAsync(catalogo.DireccionDelPaquete(paquete), archivo, ficha.Nombre, paquete.Bytes, paquete.Sha256,
                        anteriores, total, progreso, cancelacion).ConfigureAwait(false);
                    anteriores += paquete.Bytes;
                    progreso?.Report(new ProgresoDeDescarga($"Verificando {ficha.Nombre}…", anteriores, total));
                    preparados.Add(await PrepararDlcAsync(archivo, zona, destinoDlc, ficha, paquete, cancelacion).ConfigureAwait(false));
                }

                if (soundtrack is not null)
                {
                    string archivo = Path.Combine(zona, "soundtrack.zip");
                    string nombre = $"soundtrack de {ficha.Nombre}";
                    await DescargarAsync(catalogo.DireccionDelSoundtrack(soundtrack), archivo, nombre, soundtrack.Bytes, soundtrack.Sha256,
                        anteriores, total, progreso, cancelacion).ConfigureAwait(false);
                    anteriores += soundtrack.Bytes;
                    progreso?.Report(new ProgresoDeDescarga($"Verificando {nombre}…", anteriores, total));
                    string carpetaPreparada = Path.Combine(zona, "soundtrack");
                    await ExtraerSoundtrackAsync(archivo, carpetaPreparada, soundtrack.Pistas, cancelacion).ConfigureAwait(false);
                    soundtracksPreparados.Add(new SoundtrackPreparado(carpetaPreparada,
                        InstalacionDeHoi4.RutaDeSoundtrack(raiz, paquete.Carpeta), soundtrack.Pistas));
                }
            }

            cancelacion.ThrowIfCancellationRequested();
            ValidarRutasDeJuego(raiz);
            InstalacionDeHoi4 instalacionActual = InstalacionDeHoi4.Leer(raiz, cancelacion);
            if (instalacionActual.BuildId != instalacion.BuildId)
            {
                throw new InvalidDataException("Steam cambió la build de HOI4 durante la descarga.");
            }

            if (seleccion.Any(entrada => !entrada.InstalarDlc && !instalacionActual.EstaInstalado(entrada.Ficha)))
            {
                throw new IOException("La instalación de un DLC cambió durante la descarga del soundtrack.");
            }

            MetadatosDeDlc actuales = instalacionActual.Metadatos ??
                throw new InvalidDataException("Los metadatos cambiaron durante la descarga y ya no se pueden leer.");
            string? candidato = null;
            if (preparados.Count > 0)
            {
                string textoNuevo = actuales.Incorporar(preparados.Select(dlc => dlc.Entrada));
                string rutaCandidata = Path.Combine(temporal, "metadatos-nuevos.txt");
                File.WriteAllText(rutaCandidata, textoNuevo, actuales.Codificacion);
                if (!actuales.MismoContenido(MetadatosDeDlc.Leer(rutaCandidata)))
                {
                    candidato = rutaCandidata;
                }
            }
            cancelacion.ThrowIfCancellationRequested();

            progreso?.Report(new ProgresoDeDescarga("Instalando el contenido seleccionado…", total, total));
            Aplicar(raiz, preparados, soundtracksPreparados, rutaMetadatos, candidato, temporal, actuales, ref conservarRespaldo);
        }
        finally
        {
            if (!conservarRespaldo)
            {
                try
                {
                    Directory.Delete(temporal, recursive: true);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // El contenido temporal puede limpiarse después aunque otro proceso lo tenga abierto.
                }
            }
        }
    }

    private static async Task<DlcPreparado> PrepararDlcAsync(string archivo, string zona, string destino, FichaDeDlc ficha,
        PaqueteDeDlc paquete, CancellationToken cancelacion)
    {
        await ExtraerAsync(archivo, zona, paquete.Carpeta, cancelacion).ConfigureAwait(false);
        string carpetaPreparada = Path.Combine(zona, "dlc", paquete.Carpeta);
        ArchivosDeDlc archivos = DetectorDeDlc.BuscarArchivos(new DirectoryInfo(zona), cancelacion);
        DlcDetectado[] detectados = DetectorDeDlc.LeerMetadatos(archivos, cancelacion);
        if (detectados.Length != 1 || detectados[0].IdDeSteam != ficha.AppId ||
            !string.Equals(detectados[0].Carpeta, carpetaPreparada, StringComparison.OrdinalIgnoreCase) ||
            Directory.EnumerateFiles(carpetaPreparada, "*.dlc", SearchOption.AllDirectories).Skip(1).Any())
        {
            throw new InvalidDataException($"Los archivos de {ficha.Nombre} no coinciden con su App ID y carpeta.");
        }

        string rutaAprobada = Path.Combine(zona, InstalacionDeHoi4.NombreDeMetadatos);
        if (!File.Exists(rutaAprobada))
        {
            throw new InvalidDataException($"El ZIP de {ficha.Nombre} debe incluir 00_dlc_info.txt.");
        }

        EntradaDeMetadatos entrada = MetadatosDeDlc.Leer(rutaAprobada).Obtener(paquete.Metadato, ficha.AppId);
        return new DlcPreparado(carpetaPreparada, Path.Combine(destino, paquete.Carpeta), entrada);
    }

    private static async Task DescargarAsync(Uri direccion, string archivo, string nombre, long bytes, string sha256, long anteriores, long total,
        IProgress<ProgresoDeDescarga>? progreso, CancellationToken cancelacion)
    {
        string mensaje = $"Descargando {nombre}…";
        progreso?.Report(new ProgresoDeDescarga(mensaje, anteriores, total));
        using HttpResponseMessage respuesta = await CatalogoDeDlc.Cliente.GetAsync(
            direccion, HttpCompletionOption.ResponseHeadersRead, cancelacion).ConfigureAwait(false);
        respuesta.EnsureSuccessStatusCode();
        if (respuesta.Content.Headers.ContentLength is long longitud && longitud != bytes)
        {
            throw new InvalidDataException($"El tamaño publicado para {nombre} no coincide con el de la descarga.");
        }

        await using Stream origen = await respuesta.Content.ReadAsStreamAsync(cancelacion).ConfigureAwait(false);
        await using var destino = new FileStream(archivo, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536];
        long recibidos = 0;
        long ultimoAviso = 0;
        int lectura;
        while ((lectura = await origen.ReadAsync(buffer.AsMemory(), cancelacion).ConfigureAwait(false)) > 0)
        {
            if (lectura > bytes - recibidos)
            {
                throw new InvalidDataException($"La descarga de {nombre} supera el tamaño publicado.");
            }

            hash.AppendData(buffer, 0, lectura);
            await destino.WriteAsync(buffer.AsMemory(0, lectura), cancelacion).ConfigureAwait(false);
            recibidos += lectura;
            if (recibidos - ultimoAviso >= 1024 * 1024)
            {
                progreso?.Report(new ProgresoDeDescarga(mensaje, anteriores + recibidos, total));
                ultimoAviso = recibidos;
            }
        }

        if (recibidos != bytes ||
            !string.Equals(Convert.ToHexString(hash.GetHashAndReset()), sha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"La descarga de {nombre} no coincide con el tamaño o SHA-256 del catálogo.");
        }
    }

    private static async Task ExtraerAsync(string archivo, string zona, string carpeta, CancellationToken cancelacion)
    {
        using ZipArchive zip = ZipFile.OpenRead(archivo);
        string prefijo = $"dlc/{carpeta}/";
        foreach (ZipArchiveEntry entrada in zip.Entries)
        {
            cancelacion.ThrowIfCancellationRequested();
            string ruta = entrada.FullName.Replace('\\', '/');
            bool directorio = ruta.EndsWith('/');
            string[] partes = (directorio ? ruta[..^1] : ruta).Split('/');
            bool esMetadato = ruta == InstalacionDeHoi4.NombreDeMetadatos;
            bool permitido = esMetadato || ruta.StartsWith(prefijo, StringComparison.Ordinal) ||
                directorio && ruta is "dlc/";
            if (!permitido || !partes.All(CatalogoDeDlc.ComponenteValido) || EsEnlace(entrada) ||
                esMetadato && entrada.Length > MetadatosDeDlc.MaximoDeBytes)
            {
                throw new InvalidDataException("El ZIP contiene una ruta, un enlace o un archivo de metadatos inválido.");
            }

            string destino = Path.Combine(zona, Path.Combine(partes));
            if (directorio)
            {
                Directory.CreateDirectory(destino);
                continue;
            }

            await ExtraerArchivoAsync(entrada, destino, cancelacion).ConfigureAwait(false);
        }
    }

    private static async Task ExtraerSoundtrackAsync(string archivo, string carpeta, string[] pistas, CancellationToken cancelacion)
    {
        var pendientes = new HashSet<string>(pistas, StringComparer.OrdinalIgnoreCase);
        using ZipArchive zip = ZipFile.OpenRead(archivo);
        foreach (ZipArchiveEntry entrada in zip.Entries)
        {
            cancelacion.ThrowIfCancellationRequested();
            if (!pendientes.Remove(entrada.FullName) || EsEnlace(entrada) || entrada.Length <= 0)
            {
                throw new InvalidDataException("El ZIP del soundtrack contiene una pista inválida o inesperada.");
            }

            await ExtraerArchivoAsync(entrada, Path.Combine(carpeta, entrada.FullName), cancelacion).ConfigureAwait(false);
        }

        if (pendientes.Count > 0)
        {
            throw new InvalidDataException("Al ZIP del soundtrack le faltan pistas.");
        }
    }

    private static bool EsEnlace(ZipArchiveEntry entrada) =>
        (entrada.ExternalAttributes >> 16 & 0xF000) == 0xA000 ||
        (entrada.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0;

    private static async Task ExtraerArchivoAsync(ZipArchiveEntry entrada, string destino, CancellationToken cancelacion)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);
        await using Stream origen = entrada.Open();
        await using var salida = new FileStream(destino, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, useAsync: true);
        await origen.CopyToAsync(salida, cancelacion).ConfigureAwait(false);
        if (salida.Length != entrada.Length)
        {
            throw new InvalidDataException("Un archivo extraído no tiene el tamaño que indica el ZIP.");
        }
    }

    private static void Aplicar(string raiz, IReadOnlyList<DlcPreparado> preparados, IReadOnlyList<SoundtrackPreparado> soundtracks,
        string rutaMetadatos, string? candidato, string temporal, MetadatosDeDlc actuales, ref bool conservarRespaldo)
    {
        string respaldoMetadatos = Path.Combine(temporal, "respaldo-metadatos.txt");
        var movimientos = new List<Movimiento>();
        var pistasInstaladas = new List<string>();
        var carpetasDeSoundtrack = new List<string>();
        try
        {
            VerificarMetadatos(rutaMetadatos, actuales);
            foreach (DlcPreparado preparado in preparados)
            {
                ValidarDestino(raiz, preparado.Destino, preparado.Entrada.AppId, CancellationToken.None);
                Directory.CreateDirectory(Path.GetDirectoryName(preparado.Destino)!);
                var movimiento = new Movimiento(preparado, Path.Combine(temporal, $"respaldo-{movimientos.Count}"));
                movimientos.Add(movimiento);
                if (Directory.Exists(preparado.Destino))
                {
                    Directory.Move(preparado.Destino, movimiento.Respaldo);
                    movimiento.HayRespaldo = true;
                }

                Directory.Move(preparado.Carpeta, preparado.Destino);
                movimiento.CarpetaInstalada = true;
            }

            foreach (SoundtrackPreparado soundtrack in soundtracks)
            {
                ValidarDestinoSoundtrack(soundtrack.Destino, soundtrack.Pistas);
                CrearCarpetaDeSoundtrack(Path.GetDirectoryName(soundtrack.Destino)!);
                CrearCarpetaDeSoundtrack(soundtrack.Destino);
                foreach (string pista in soundtrack.Pistas)
                {
                    string destino = Path.Combine(soundtrack.Destino, pista);
                    if (File.Exists(destino))
                    {
                        continue;
                    }

                    File.Move(Path.Combine(soundtrack.Carpeta, pista), destino);
                    pistasInstaladas.Add(destino);
                }
            }

            if (candidato is not null)
            {
                File.Replace(candidato, rutaMetadatos, respaldoMetadatos);
                VerificarMetadatos(respaldoMetadatos, actuales);
            }
            else
            {
                VerificarMetadatos(rutaMetadatos, actuales);
            }
        }
        catch (Exception fallo) when (fallo is IOException or UnauthorizedAccessException or InvalidDataException or DecoderFallbackException)
        {
            try
            {
                if (File.Exists(respaldoMetadatos))
                {
                    File.Move(respaldoMetadatos, rutaMetadatos, overwrite: true);
                }

                foreach (string pista in pistasInstaladas)
                {
                    File.Delete(pista);
                }

                for (int indice = carpetasDeSoundtrack.Count - 1; indice >= 0; indice--)
                {
                    string carpeta = carpetasDeSoundtrack[indice];
                    if (Directory.Exists(carpeta) && !Directory.EnumerateFileSystemEntries(carpeta).Any())
                    {
                        Directory.Delete(carpeta);
                    }
                }

                for (int indice = movimientos.Count - 1; indice >= 0; indice--)
                {
                    Movimiento movimiento = movimientos[indice];
                    if (movimiento.CarpetaInstalada)
                    {
                        Directory.Move(movimiento.Dlc.Destino, movimiento.Dlc.Carpeta);
                    }

                    if (movimiento.HayRespaldo)
                    {
                        Directory.Move(movimiento.Respaldo, movimiento.Dlc.Destino);
                    }
                }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                conservarRespaldo = true;
                throw new IOException($"No se pudo restaurar la instalación. Los archivos de respaldo están en {temporal}.",
                    new AggregateException(fallo, error));
            }

            throw;
        }

        void CrearCarpetaDeSoundtrack(string carpeta)
        {
            if (!Directory.Exists(carpeta))
            {
                Directory.CreateDirectory(carpeta);
                carpetasDeSoundtrack.Add(carpeta);
            }
        }
    }

    private static void VerificarMetadatos(string ruta, MetadatosDeDlc esperados)
    {
        if (!MetadatosDeDlc.Leer(ruta).MismoContenido(esperados))
        {
            throw new IOException("Los metadatos cambiaron durante la instalación. Vuelve a intentarlo.");
        }
    }

    private static void ValidarRutasDeJuego(string raiz)
    {
        ValidarRuta(Path.Combine(raiz, "dlc"));
        ValidarRuta(Path.Combine(raiz, "dlc_metadata"));
        ValidarRuta(Path.Combine(raiz, "dlc_metadata", "dlc_info"));
        ValidarRuta(InstalacionDeHoi4.RutaDeMetadatos(raiz));
    }

    private static void ValidarDestino(string raiz, string destino, uint? appId, CancellationToken cancelacion)
    {
        ValidarRuta(destino);
        if (File.Exists(destino))
        {
            throw new InvalidDataException($"La ruta {Path.GetFileName(destino)} está ocupada por un archivo.");
        }

        if (!Directory.Exists(destino) || !Directory.EnumerateFileSystemEntries(destino).Any())
        {
            return;
        }

        ArchivosDeDlc archivos = DetectorDeDlc.BuscarArchivos(new DirectoryInfo(raiz), cancelacion);
        DlcDetectado[] existentes = DetectorDeDlc.LeerMetadatos(archivos, cancelacion)
            .Where(dlc => string.Equals(dlc.Carpeta, destino, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (existentes.Length == 0 || existentes.Any(dlc => dlc.IdDeSteam != appId))
        {
            throw new InvalidDataException($"La carpeta {Path.GetFileName(destino)} está ocupada por contenido que no coincide con este DLC.");
        }
    }

    private static void ValidarDestinoSoundtrack(string destino, string[] pistas)
    {
        string carpeta = Path.GetDirectoryName(destino)!;
        ValidarRuta(carpeta);
        ValidarRuta(destino);
        if (File.Exists(carpeta) || File.Exists(destino))
        {
            throw new IOException($"La ruta del soundtrack está ocupada por un archivo: {destino}");
        }

        foreach (string pista in pistas)
        {
            string archivo = Path.Combine(destino, pista);
            ValidarRuta(archivo);
            if (Directory.Exists(archivo))
            {
                throw new IOException($"La ruta de la pista está ocupada por una carpeta: {archivo}");
            }
        }
    }

    private static void ValidarRuta(string ruta)
    {
        try
        {
            if ((File.GetAttributes(ruta) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException($"La ruta de instalación es un enlace: {ruta}");
            }
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
        }
    }

    private sealed record DlcPreparado(string Carpeta, string Destino, EntradaDeMetadatos Entrada);
    private sealed record SoundtrackPreparado(string Carpeta, string Destino, string[] Pistas);

    private sealed class Movimiento(DlcPreparado dlc, string respaldo)
    {
        public DlcPreparado Dlc { get; } = dlc;
        public string Respaldo { get; } = respaldo;
        public bool HayRespaldo { get; set; }
        public bool CarpetaInstalada { get; set; }
    }
}
