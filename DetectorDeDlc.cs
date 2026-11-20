using System.Globalization;
using System.Text.RegularExpressions;

namespace DlcDeHoi4;

internal sealed record DlcDetectado(string Nombre, uint? IdDeSteam, bool EstaIntegrado, string? ImagenLocal, string? Carpeta = null);
internal sealed record ArchivosDeDlc(string[] Instalados, string[] Integrados, DlcDetectado[] Extras);

internal static partial class DetectorDeDlc
{
    public const string CarpetaDelJuego = @"C:\Program Files (x86)\Steam\steamapps\common\Hearts of Iron IV";

    private static readonly (uint Id, string Nombre, string Carpeta, string Subcarpeta, string Patron, string? Imagen)[] Extras =
    [
        (472410, "Wallpaper", "dlc014_wallpaper", "", "*.png", "PreOrderReward_wallpaper-1280x720.png"),
        (472411, "Unique Wallpaper", "dlc015_unique_wallpaper", "", "Signed-SignupReward_wallpaper-*.png",
            "Signed-SignupReward_wallpaper-1920x1080.png"),
        (473130, "Artbook", "dlc016_artbook", "", "Hearts of Iron IV - Art of War.pdf", null),
        (486950, "Original Soundtrack", "dlc017_original_soundtrack", "MP3", "*.mp3", null),
        (1032150, "Man the Guns Wallpaper", "dlc024_man_the_guns_wallpaper", "", "WhatDamageWecan-*.png",
            "WhatDamageWecan-1920x1080.png"),
    ];

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

        return new ArchivosDeDlc(instalados, integrados, BuscarExtras(carpetaDeDlc, cancelacion).ToArray());
    }

    public static DlcDetectado[] LeerMetadatos(ArchivosDeDlc archivos, CancellationToken cancelacion)
    {
        IEnumerable<DlcDetectado> instalados = LeerDescriptores(archivos.Instalados, estaIntegrado: false, cancelacion);
        return instalados
            .Concat(archivos.Extras)
            .Concat(LeerDescriptores(archivos.Integrados, estaIntegrado: true, cancelacion))
            .DistinctBy(dlc => (dlc.IdDeSteam, dlc.Nombre))
            .OrderBy(dlc => dlc.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<DlcDetectado> BuscarExtras(string carpetaDeDlc, CancellationToken cancelacion)
    {
        var opciones = new EnumerationOptions
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false,
        };
        foreach (var (id, nombre, carpeta, subcarpeta, patron, imagenPreferida) in Extras)
        {
            cancelacion.ThrowIfCancellationRequested();
            string ruta = Path.Combine(carpetaDeDlc, carpeta);
            string contenido = Path.Combine(ruta, subcarpeta);
            if (!Directory.Exists(contenido) || (File.GetAttributes(ruta) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(contenido) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            string[] archivos = Directory.GetFiles(contenido, patron, opciones);
            if (archivos.Length > 0)
            {
                string? imagen = imagenPreferida is null ? null :
                    archivos.FirstOrDefault(archivo => Path.GetFileName(archivo).Equals(imagenPreferida, StringComparison.OrdinalIgnoreCase))
                    ?? archivos[0];
                yield return new DlcDetectado(nombre, id, EstaIntegrado: false, imagen, ruta);
            }
        }
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

        string carpeta = Path.GetDirectoryName(ruta)!;
        string miniatura = Path.Combine(carpeta, "thumbnail.png");
        return new DlcDetectado(nombre, idDeSteam, estaIntegrado, File.Exists(miniatura) ? miniatura : null, carpeta);
    }

    [GeneratedRegex("""^\s*(?<clave>name|steam_id)\s*=\s*(?:"(?<valor>(?:\\.|[^"\\])*)"|(?<valor>[^#" \t]+))\s*(?:#.*)?$""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PatronDeCampo();
}
