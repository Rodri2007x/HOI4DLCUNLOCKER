using System.Text.RegularExpressions;

namespace DlcDeHoi4;

internal sealed partial class InstalacionDeHoi4
{
    public const string NombreDeMetadatos = "00_dlc_info.txt";
    public string Carpeta { get; }
    public string? BuildId { get; }
    public MetadatosDeDlc? Metadatos { get; }
    public DlcDetectado[] Detectados { get; }

    private InstalacionDeHoi4(string carpeta, string? buildId, MetadatosDeDlc? metadatos, DlcDetectado[] detectados)
    {
        Carpeta = carpeta;
        BuildId = buildId;
        Metadatos = metadatos;
        Detectados = detectados;
    }

    public static InstalacionDeHoi4 Leer(string carpeta, CancellationToken cancelacion)
    {
        DirectoryInfo raiz = DetectorDeDlc.VerificarInstalacion(carpeta);
        ArchivosDeDlc archivos = DetectorDeDlc.BuscarArchivos(raiz, cancelacion);
        DlcDetectado[] detectados = DetectorDeDlc.LeerMetadatos(archivos, cancelacion);
        MetadatosDeDlc? metadatos = null;
        try
        {
            if (File.Exists(RutaDeMetadatos(raiz.FullName)))
            {
                metadatos = MetadatosDeDlc.Leer(RutaDeMetadatos(raiz.FullName));
            }
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.DecoderFallbackException)
        {
            // Sigue siendo posible mostrar los DLC detectados mientras se repara el archivo.
        }

        return new InstalacionDeHoi4(raiz.FullName, LeerBuildId(raiz.FullName), metadatos, detectados);
    }

    public bool EstaInstalado(FichaDeDlc ficha, bool incluirSoundtracks = false)
    {
        foreach (DlcDetectado dlc in Detectados.Where(dlc => dlc.IdDeSteam == ficha.AppId))
        {
            if (dlc.EstaIntegrado || ficha.Paquete is null ||
                string.Equals(dlc.Carpeta, Path.Combine(Carpeta, "dlc", ficha.Paquete.Carpeta), StringComparison.OrdinalIgnoreCase) &&
                Metadatos?.Contiene(ficha.Paquete.Metadato, ficha.AppId) == true)
            {
                return !incluirSoundtracks || ficha.Paquete is null || SoundtrackInstalado(ficha.Paquete);
            }
        }

        return false;
    }

    public bool SePuedeInstalar(FichaDeDlc ficha, bool incluirSoundtracks = false) =>
        ficha.Paquete is not null && BuildId == ficha.Paquete.BuildId && Metadatos is not null &&
        !EstaInstalado(ficha, incluirSoundtracks);

    public bool SoundtrackInstalado(PaqueteDeDlc paquete) =>
        paquete.Soundtrack is null || paquete.Soundtrack.Pistas.All(pista =>
            File.Exists(Path.Combine(RutaDeSoundtrack(Carpeta, paquete.Carpeta), pista)));

    public static string RutaDeSoundtrack(string carpeta, string carpetaDlc) =>
        Path.Combine(carpeta, "soundtrack", carpetaDlc);

    public static string RutaDeMetadatos(string carpeta) =>
        Path.Combine(carpeta, "dlc_metadata", "dlc_info", NombreDeMetadatos);

    public static string? LeerBuildId(string carpeta)
    {
        string ruta = Path.GetFullPath(Path.Combine(carpeta, "..", "..", "appmanifest_394360.acf"));
        if (!File.Exists(ruta))
        {
            return null;
        }

        foreach (string linea in File.ReadLines(ruta))
        {
            Match build = PatronDeBuildId().Match(linea);
            if (build.Success)
            {
                return build.Groups["id"].Value;
            }
        }

        return null;
    }

    [GeneratedRegex("""^\s*"buildid"\s*"(?<id>[0-9]+)"\s*$""", RegexOptions.CultureInvariant)]
    private static partial Regex PatronDeBuildId();
}
