using System.Globalization;
using System.Text.RegularExpressions;

namespace DlcDeHoi4;

internal sealed record DlcDetectado(string Nombre, uint? IdDeSteam, bool EstaIntegrado, string? ImagenLocal);
internal sealed record ArchivosDeDlc(string[] Instalados, string[] Integrados, string? ImagenWallpaper);

internal static partial class DetectorDeDlc
{
    public const string CarpetaDelJuego = @"C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV";

    public static DirectoryInfo VerificarInstalacion(string carpetaDelJuego)
    {
        var instalacion = new DirectoryInfo(carpetaDelJuego);
        if (!instalacion.Exists)
        {
            throw new DirectoryNotFoundException("No se encontró la carpeta de Hearts of Iron IV.");
        }

        return instalacion;
    }

    public static ArchivosDeDlc BuscarArchivos(DirectoryInfo instalacion, CancellationToken cancelacion)
    {
        string carpetaDeDlc = Path.Combine(instalacion.FullName, "dlc");
        string carpetaDeIntegrados = Path.Combine(instalacion.FullName, "integrated_dlc");
        string[] instalados = BuscarDescriptores(carpetaDeDlc, cancelacion).ToArray();
        string[] integrados = BuscarDescriptores(carpetaDeIntegrados, cancelacion).ToArray();

        // Steam distribuye el pack Wallpaper como archivos PNG.
        string carpetaDeFondos = Path.Combine(carpetaDeDlc, "dlc014_wallpaper");
        cancelacion.ThrowIfCancellationRequested();
        string? imagenWallpaper = null;
        if (Directory.Exists(carpetaDeFondos))
        {
            string fondoPreferido = Path.Combine(carpetaDeFondos, "PreOrderReward_wallpaper-1280x720.png");
            imagenWallpaper = File.Exists(fondoPreferido)
                ? fondoPreferido
                : Directory.EnumerateFiles(carpetaDeFondos, "*.png").FirstOrDefault();
        }

        return new ArchivosDeDlc(instalados, integrados, imagenWallpaper);
    }

    public static DlcDetectado[] LeerMetadatos(ArchivosDeDlc archivos, CancellationToken cancelacion)
    {
        IEnumerable<DlcDetectado> instalados = LeerDescriptores(archivos.Instalados, estaIntegrado: false, cancelacion);
        if (archivos.ImagenWallpaper is string imagenWallpaper)
        {
            instalados = instalados.Append(new DlcDetectado("Wallpaper", 472410, EstaIntegrado: false, imagenWallpaper));
        }

        return instalados
            .Concat(LeerDescriptores(archivos.Integrados, estaIntegrado: true, cancelacion))
            .DistinctBy(dlc => (dlc.IdDeSteam, dlc.Nombre))
            .OrderBy(dlc => dlc.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<string> BuscarDescriptores(string carpeta, CancellationToken cancelacion)
    {
        if (!Directory.Exists(carpeta))
        {
            yield break;
        }

        var opciones = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 1,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
        };

        foreach (string descriptor in Directory.EnumerateFiles(carpeta, "*.dlc", opciones))
        {
            cancelacion.ThrowIfCancellationRequested();
            yield return descriptor;
        }
    }

    private static IEnumerable<DlcDetectado> LeerDescriptores(
        IEnumerable<string> descriptores, bool estaIntegrado, CancellationToken cancelacion)
    {
        foreach (string descriptor in descriptores)
        {
            cancelacion.ThrowIfCancellationRequested();
            if (LeerDescriptor(descriptor, estaIntegrado) is DlcDetectado dlc)
            {
                yield return dlc;
            }
        }
    }

    private static DlcDetectado? LeerDescriptor(string ruta, bool estaIntegrado)
    {
        string? nombre = null;
        uint? idDeSteam = null;

        foreach (string linea in File.ReadLines(ruta))
        {
            Match campo = PatronDeCampo().Match(linea);
            if (!campo.Success)
            {
                continue;
            }

            string valor = campo.Groups["valor"].Value;
            if (campo.Groups["clave"].Value.Equals("name", StringComparison.OrdinalIgnoreCase))
            {
                nombre = valor.Replace("\\\"", "\"", StringComparison.Ordinal).Trim();
            }
            else if (uint.TryParse(valor, NumberStyles.None, CultureInfo.InvariantCulture, out uint id) && id > 0)
            {
                idDeSteam = id;
            }
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return null;
        }

        string miniatura = Path.Combine(Path.GetDirectoryName(ruta)!, "thumbnail.png");
        return new DlcDetectado(nombre, idDeSteam, estaIntegrado, File.Exists(miniatura) ? miniatura : null);
    }

    [GeneratedRegex("""^\s*(?<clave>name|steam_id)\s*=\s*(?:"(?<valor>(?:\\.|[^"\\])*)"|(?<valor>[^#" \t]+))\s*(?:#.*)?$""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PatronDeCampo();
}
