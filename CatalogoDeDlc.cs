using System.Text;
using System.Text.Json;

namespace DlcDeHoi4;

internal sealed record FichaDeDlc(uint AppId, string Nombre, PaqueteDeDlc? Paquete);
internal sealed record PaqueteDeDlc(string BuildId, string Carpeta, string Metadato, string Archivo, long Bytes, string Sha256,
    SoundtrackDeDlc? Soundtrack = null);
internal sealed record SoundtrackDeDlc(string Archivo, long Bytes, string Sha256, string[] Pistas);

internal sealed class CatalogoDeDlc
{
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web);
    private readonly Uri baseDeArchivos;

    internal static HttpClient Cliente { get; } = new(new HttpClientHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
    })
    {
        Timeout = TimeSpan.FromSeconds(30),
        MaxResponseContentBufferSize = 1024 * 1024,
    };

    public FichaDeDlc[] Dlcs { get; }

    private CatalogoDeDlc(Uri direccion, FichaDeDlc[] dlcs)
    {
        baseDeArchivos = new Uri(direccion, ".");
        Dlcs = dlcs;
    }

    public static Uri LeerDireccion(string texto)
    {
        if (Uri.TryCreate(texto, UriKind.Absolute, out Uri? direccion) &&
            (direccion.Scheme == Uri.UriSchemeHttps || direccion.Scheme == Uri.UriSchemeHttp && direccion.IsLoopback) &&
            direccion.Host.Length > 0 && direccion.UserInfo.Length == 0 && direccion.Fragment.Length == 0)
        {
            return direccion;
        }

        throw new InvalidDataException("Usa una URL HTTPS para el catálogo o HTTP en localhost para una prueba.");
    }

    public static async Task<CatalogoDeDlc> CargarAsync(Uri direccion, CancellationToken cancelacion)
    {
        LeerDireccion(direccion.AbsoluteUri);
        using HttpResponseMessage respuesta = await Cliente.GetAsync(direccion, cancelacion).ConfigureAwait(false);
        respuesta.EnsureSuccessStatusCode();
        byte[] bytes = await respuesta.Content.ReadAsByteArrayAsync(cancelacion).ConfigureAwait(false);
        ReadOnlySpan<byte> json = bytes;
        if (json.StartsWith(Encoding.UTF8.Preamble))
        {
            json = json[Encoding.UTF8.Preamble.Length..];
        }

        var documento = JsonSerializer.Deserialize<Documento>(json, OpcionesJson);
        if (documento is not { Version: 1, Dlcs: not null })
        {
            throw new InvalidDataException("El catálogo debe tener versión 1 y una lista de DLC.");
        }

        var catalogo = new CatalogoDeDlc(direccion, documento.Dlcs);
        var ids = new HashSet<uint>();
        var carpetas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var claves = new HashSet<string>(StringComparer.Ordinal);
        foreach (FichaDeDlc ficha in catalogo.Dlcs)
        {
            if (ficha is null || ficha.AppId == 0 || string.IsNullOrWhiteSpace(ficha.Nombre) || !ids.Add(ficha.AppId))
            {
                throw new InvalidDataException("El catálogo contiene un DLC sin nombre o un App ID inválido o repetido.");
            }

            if (ficha.Paquete is PaqueteDeDlc paquete)
            {
                if (paquete.BuildId is not { Length: > 0 } || !paquete.BuildId.All(char.IsAsciiDigit) ||
                    !ComponenteValido(paquete.Carpeta) || !carpetas.Add(paquete.Carpeta) ||
                    !ComponenteValido(paquete.Metadato) || !paquete.Metadato.All(c => char.IsAsciiLetterOrDigit(c) || c == '_') ||
                    !claves.Add(paquete.Metadato) || !DescargaValida(paquete.Bytes, paquete.Sha256))
                {
                    throw new InvalidDataException($"Los datos del paquete de {ficha.Nombre} son inválidos o se repiten.");
                }

                catalogo.DireccionDelPaquete(paquete);
                if (paquete.Soundtrack is SoundtrackDeDlc soundtrack)
                {
                    if (!DescargaValida(soundtrack.Bytes, soundtrack.Sha256) || soundtrack.Pistas is not { Length: > 0 } ||
                        soundtrack.Pistas.Any(pista => !ComponenteValido(pista) ||
                            !pista.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)) ||
                        soundtrack.Pistas.Distinct(StringComparer.OrdinalIgnoreCase).Count() != soundtrack.Pistas.Length)
                    {
                        throw new InvalidDataException($"Los datos del soundtrack de {ficha.Nombre} son inválidos.");
                    }

                    catalogo.DireccionDelSoundtrack(soundtrack);
                }
            }
        }

        return catalogo;
    }

    public Uri DireccionDelPaquete(PaqueteDeDlc paquete) => DireccionDelZip(paquete.Archivo);

    public Uri DireccionDelSoundtrack(SoundtrackDeDlc soundtrack) => DireccionDelZip(soundtrack.Archivo);

    private Uri DireccionDelZip(string archivo)
    {
        if (archivo is { Length: > 0 } && !archivo.Contains('\\') &&
            Uri.TryCreate(archivo, UriKind.Relative, out Uri? relativa))
        {
            var direccion = new Uri(baseDeArchivos, relativa);
            if (direccion.Scheme == baseDeArchivos.Scheme && direccion.Host == baseDeArchivos.Host &&
                direccion.Port == baseDeArchivos.Port && direccion.UserInfo.Length == 0 &&
                direccion.Fragment.Length == 0 && baseDeArchivos.IsBaseOf(direccion) &&
                direccion.AbsolutePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return direccion;
            }
        }

        throw new InvalidDataException("El ZIP debe tener una ruta relativa dentro de la carpeta del catálogo.");
    }

    private static bool DescargaValida(long bytes, string? sha256) =>
        bytes > 0 && sha256 is { Length: 64 } && sha256.All(Uri.IsHexDigit);

    internal static bool ComponenteValido(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre) || nombre is "." or ".." ||
            nombre.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || nombre.EndsWith(' ') || nombre.EndsWith('.'))
        {
            return false;
        }

        string raiz = nombre.Split('.', 2)[0].TrimEnd().ToUpperInvariant();
        return raiz is not ("CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$") &&
            !(raiz.Length == 4 && (raiz.StartsWith("COM", StringComparison.Ordinal) ||
                raiz.StartsWith("LPT", StringComparison.Ordinal)) && "0123456789¹²³".Contains(raiz[3]));
    }

    private sealed record Documento(int Version, FichaDeDlc[]? Dlcs);
}
