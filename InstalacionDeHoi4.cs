using System.Text.Json;
using System.Text.RegularExpressions;

namespace DlcDeHoi4;

internal sealed partial class InstalacionDeHoi4
{
    public const string NombreDeMetadatos = "00_dlc_info.txt";
    public string Carpeta { get; }
    public string? BuildId { get; }
    public string? VersionDelJuego { get; }
    public MetadatosDeDlc? Metadatos { get; }
    public DlcDetectado[] Detectados { get; }

    private InstalacionDeHoi4(string carpeta, string? buildId, string? versionDelJuego, MetadatosDeDlc? metadatos, DlcDetectado[] detectados)
    {
        Carpeta = carpeta;
        BuildId = buildId;
        VersionDelJuego = versionDelJuego;
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

        return new InstalacionDeHoi4(raiz.FullName, LeerBuildId(raiz.FullName), LeerVersionDelJuego(raiz.FullName), metadatos, detectados);
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

    private static string? LeerVersionDelJuego(string carpeta)
    {
        try
        {
            string ruta = Path.Combine(carpeta, "launcher-settings.json");
            using JsonDocument configuracion = JsonDocument.Parse(File.ReadAllText(ruta));
            JsonElement raiz = configuracion.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (string clave in new[] { "version", "rawVersion" })
            {
                if (raiz.TryGetProperty(clave, out JsonElement valor) && valor.ValueKind == JsonValueKind.String)
                {
                    Match version = PatronDeVersion().Match(valor.GetString()!);
                    if (version.Success)
                    {
                        return $"v{version.Groups["numero"].Value}";
                    }
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            // El Build ID de Steam sigue disponible si el lanzador no expone la versión.
        }

        return null;
    }

    [GeneratedRegex("""^\s*"buildid"\s*"(?<id>[0-9]+)"\s*$""", RegexOptions.CultureInvariant)]
    private static partial Regex PatronDeBuildId();

    [GeneratedRegex(@"\bv?(?<numero>[0-9]+(?:\.[0-9]+)+)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex PatronDeVersion();
}
