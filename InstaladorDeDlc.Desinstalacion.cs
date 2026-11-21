namespace DlcDeHoi4;

internal static partial class InstaladorDeDlc
{
    public static void Desinstalar(string carpeta, IEnumerable<FichaDeDlc> fichas,
        IProgress<ProgresoDeDescarga>? progreso, CancellationToken cancelacion, bool incluirSoundtracks)
    {
        cancelacion.ThrowIfCancellationRequested();
        progreso?.Report(new ProgresoDeDescarga("Comprobando los DLC seleccionados…", 0));
        string raiz = DetectorDeDlc.VerificarInstalacion(carpeta).FullName;
        ValidarRutasDeJuego(raiz);
        using var bloqueo = BloquearInstalacion(raiz);
        InstalacionDeHoi4 instalacion = InstalacionDeHoi4.Leer(raiz, cancelacion);
        MetadatosDeDlc actuales = instalacion.Metadatos ??
            throw new InvalidDataException("Falta un 00_dlc_info.txt válido en la instalación de HOI4.");
        var preparados = new List<DlcPreparado>();
        var soundtracks = new List<SoundtrackPreparado>();
        foreach (FichaDeDlc ficha in fichas.DistinctBy(ficha => ficha.AppId))
        {
            cancelacion.ThrowIfCancellationRequested();
            if (ficha.Paquete is not PaqueteDeDlc paquete || !CatalogoDeDlc.ComponenteValido(paquete.Carpeta) ||
                !instalacion.SePuedeDesinstalar(ficha))
            {
                throw new InvalidDataException($"La instalación de {ficha.Nombre} cambió o no se puede desinstalar.");
            }

            string destino = Path.Combine(raiz, "dlc", paquete.Carpeta);
            ValidarDestino(raiz, destino, ficha.AppId, cancelacion);
            preparados.Add(new DlcPreparado(null, destino, actuales.Obtener(paquete.Metadato, ficha.AppId)));

            if (incluirSoundtracks && paquete.Soundtrack is SoundtrackDeDlc soundtrack)
            {
                if (!soundtrack.Pistas.All(CatalogoDeDlc.ComponenteValido))
                {
                    throw new InvalidDataException($"El soundtrack de {ficha.Nombre} contiene un nombre de pista inválido.");
                }

                string ruta = InstalacionDeHoi4.RutaDeSoundtrack(raiz, paquete.Carpeta);
                ValidarDestinoSoundtrack(ruta, soundtrack.Pistas);
                soundtracks.Add(new SoundtrackPreparado(null, ruta, soundtrack.Pistas));
            }
        }

        if (preparados.Count == 0)
        {
            return;
        }

        string rutaMetadatos = InstalacionDeHoi4.RutaDeMetadatos(raiz);
        string temporal = Directory.CreateDirectory(Path.Combine(raiz, $".dlcdehoi4-{Guid.NewGuid():N}")).FullName;
        bool conservarRespaldo = false;
        try
        {
            string candidato = Path.Combine(temporal, "metadatos-nuevos.txt");
            File.WriteAllText(candidato, actuales.Retirar(preparados.Select(dlc => dlc.Entrada)), actuales.Codificacion);
            _ = MetadatosDeDlc.Leer(candidato);
            cancelacion.ThrowIfCancellationRequested();
            Aplicar(raiz, preparados, soundtracks, rutaMetadatos, candidato, temporal, actuales, progreso,
                ref conservarRespaldo, desinstalando: true);
        }
        finally
        {
            LimpiarTemporal(raiz, temporal, conservarRespaldo);
        }
    }
}
