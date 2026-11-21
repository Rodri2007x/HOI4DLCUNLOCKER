using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DlcDeHoi4;

internal static partial class LocalizadorDeHoi4
{
    public static string? Buscar(CancellationToken cancelacion) => BuscarEnBibliotecas(RaicesDeSteam(), cancelacion);

    internal static string? BuscarEnBibliotecas(IEnumerable<string?> raices, CancellationToken cancelacion)
    {
        var revisadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string? raiz in raices)
        {
            cancelacion.ThrowIfCancellationRequested();
            if (NormalizarRuta(raiz) is not string steam)
            {
                continue;
            }

            IEnumerable<string> bibliotecas = LeerCampos(Path.Combine(steam, "steamapps", "libraryfolders.vdf"))
                .Where(campo => campo.Clave is "path" || campo.Clave.All(char.IsAsciiDigit))
                .Select(campo => campo.Valor)
                .Prepend(steam);
            foreach (string biblioteca in bibliotecas)
            {
                cancelacion.ThrowIfCancellationRequested();
                if (NormalizarRuta(biblioteca) is not string ruta || !revisadas.Add(ruta))
                {
                    continue;
                }

                string steamapps = Path.Combine(ruta, "steamapps");
                string? nombre = LeerCampos(Path.Combine(steamapps, "appmanifest_394360.acf"))
                    .FirstOrDefault(campo => campo.Clave == "installdir").Valor;
                string juego = Path.Combine(steamapps, "common",
                    nombre is not null && CatalogoDeDlc.ComponenteValido(nombre) ? nombre : "Hearts of Iron IV");
                if (EsCarpetaDelJuego(juego))
                {
                    return juego;
                }
            }
        }

        return null;
    }

    public static bool EsCarpetaDelJuego(string carpeta) => File.Exists(Path.Combine(carpeta, "hoi4.exe"));

    private static IEnumerable<string?> RaicesDeSteam()
    {
        yield return LeerRegistro(RegistryHive.CurrentUser, RegistryView.Default, "SteamPath");
        yield return LeerRegistro(RegistryHive.LocalMachine, RegistryView.Registry32, "InstallPath");
        if (Environment.Is64BitOperatingSystem)
        {
            yield return LeerRegistro(RegistryHive.LocalMachine, RegistryView.Registry64, "InstallPath");
        }

        foreach (Environment.SpecialFolder especial in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles })
        {
            string carpeta = Environment.GetFolderPath(especial);
            if (carpeta.Length > 0)
            {
                yield return Path.Combine(carpeta, "Steam");
            }
        }
    }

    private static string? LeerRegistro(RegistryHive hive, RegistryView vista, string valor)
    {
        try
        {
            using RegistryKey rama = RegistryKey.OpenBaseKey(hive, vista);
            using RegistryKey? steam = rama.OpenSubKey(@"Software\Valve\Steam");
            return steam?.GetValue(valor) as string;
        }
        catch (Exception error) when (error is IOException or SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? NormalizarRuta(string? ruta)
    {
        if (string.IsNullOrWhiteSpace(ruta) || !Path.IsPathFullyQualified(ruta))
        {
            return null;
        }

        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(ruta));
        }
        catch (Exception error) when (error is ArgumentException or IOException or NotSupportedException)
        {
            return null;
        }
    }

    private static IEnumerable<(string Clave, string Valor)> LeerCampos(string archivo)
    {
        try
        {
            return File.ReadAllLines(archivo)
                .Select(linea => PatronDeCampo().Match(linea))
                .Where(campo => campo.Success)
                .Select(campo => (campo.Groups["clave"].Value,
                    campo.Groups["valor"].Value.Replace(@"\\", @"\", StringComparison.Ordinal)));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    [GeneratedRegex("""^\s*"(?<clave>path|installdir|[0-9]+)"\s*"(?<valor>(?:\\.|[^"\\])*)"\s*(?://.*)?$""", RegexOptions.CultureInvariant)]
    private static partial Regex PatronDeCampo();
}
