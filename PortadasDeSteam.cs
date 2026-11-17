using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DlcDeHoi4;

internal static class PortadasDeSteam
{
    private const int MaximoDeBytesDeTienda = 2 * 1024 * 1024;
    private const int MaximoDeBytesLocal = 16 * 1024 * 1024;
    private static readonly HttpClient Cliente = new()
    {
        Timeout = TimeSpan.FromSeconds(6),
        MaxResponseContentBufferSize = MaximoDeBytesDeTienda,
    };
    private static readonly string CarpetaDeCache = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DlcDeHoi4", "PortadasSteam");

    public static Task<Bitmap?> LeerCacheAsync(uint id, Size tamano, CancellationToken cancelacion) =>
        LeerMiniaturaAsync(RutaDeCache(id), tamano, MaximoDeBytesDeTienda, cancelacion);

    public static Task<Bitmap?> LeerImagenLocalAsync(string ruta, Size tamano, CancellationToken cancelacion) =>
        LeerMiniaturaAsync(ruta, tamano, MaximoDeBytesLocal, cancelacion);

    public static async Task<Bitmap?> DescargarAsync(uint id, Size tamano, CancellationToken cancelacion)
    {
        string appId = id.ToString(CultureInfo.InvariantCulture);
        try
        {
            var portadaDirecta = new Uri($"https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{appId}/header.jpg");
            byte[]? datos = await DescargarImagenAsync(portadaDirecta, cancelacion).ConfigureAwait(false);
            if (datos is null)
            {
                // La ficha de Steam devuelve la ruta con hash de las portadas más recientes.
                Uri? portadaDeTienda = await BuscarEnTiendaAsync(appId, cancelacion).ConfigureAwait(false);
                if (portadaDeTienda is not null)
                {
                    datos = await DescargarImagenAsync(portadaDeTienda, cancelacion).ConfigureAwait(false);
                }
            }

            if (datos is null)
            {
                return null;
            }

            Bitmap? miniatura = CrearMiniatura(datos, tamano);
            if (miniatura is not null)
            {
                GuardarEnCache(RutaDeCache(id), datos);
            }

            return miniatura;
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or IOException or JsonException ||
            excepcion is OperationCanceledException && !cancelacion.IsCancellationRequested)
        {
            return null;
        }
    }

    private static async Task<byte[]?> DescargarImagenAsync(Uri direccion, CancellationToken cancelacion)
    {
        using HttpResponseMessage respuesta = await Cliente.GetAsync(direccion, cancelacion).ConfigureAwait(false);
        if (!respuesta.IsSuccessStatusCode ||
            respuesta.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) != true)
        {
            return null;
        }

        return await respuesta.Content.ReadAsByteArrayAsync(cancelacion).ConfigureAwait(false);
    }

    private static async Task<Uri?> BuscarEnTiendaAsync(string appId, CancellationToken cancelacion)
    {
        string direccion = $"https://store.steampowered.com/api/appdetails?appids={appId}&filters=basic";
        using HttpResponseMessage respuesta = await Cliente.GetAsync(direccion, cancelacion).ConfigureAwait(false);
        if (!respuesta.IsSuccessStatusCode)
        {
            return null;
        }

        byte[] cuerpo = await respuesta.Content.ReadAsByteArrayAsync(cancelacion).ConfigureAwait(false);
        var fichas = JsonSerializer.Deserialize<Dictionary<string, FichaDeSteam>>(cuerpo);
        if (fichas?.GetValueOrDefault(appId) is { Disponible: true, Datos.Portada: { } portada } &&
            Uri.TryCreate(portada, UriKind.Absolute, out Uri? uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            uri.Host.EndsWith(".steamstatic.com", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.Contains($"/steam/apps/{appId}/", StringComparison.Ordinal))
        {
            return uri;
        }

        return null;
    }

    private static string RutaDeCache(uint id) =>
        Path.Combine(CarpetaDeCache, id.ToString(CultureInfo.InvariantCulture) + ".jpg");

    private static async Task<Bitmap?> LeerMiniaturaAsync(
        string ruta, Size tamano, int maximoDeBytes, CancellationToken cancelacion)
    {
        try
        {
            var archivo = new FileInfo(ruta);
            if (!archivo.Exists || archivo.Length > maximoDeBytes)
            {
                return null;
            }

            byte[] datos = await File.ReadAllBytesAsync(ruta, cancelacion).ConfigureAwait(false);
            return datos.Length <= maximoDeBytes ? CrearMiniatura(datos, tamano) : null;
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void GuardarEnCache(string ruta, byte[] datos)
    {
        string temporal = $"{ruta}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(CarpetaDeCache);
            File.WriteAllBytes(temporal, datos);
            File.Move(temporal, ruta, overwrite: true);
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporal);
            }
            catch (Exception errorDeLimpieza) when (errorDeLimpieza is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    private static Bitmap? CrearMiniatura(byte[] datos, Size tamano)
    {
        if (datos.Length == 0)
        {
            return null;
        }

        Bitmap? miniatura = null;
        try
        {
            using var flujo = new MemoryStream(datos);
            using var original = Image.FromStream(flujo, useEmbeddedColorManagement: false, validateImageData: true);
            int margen = Math.Max(2, tamano.Height / 24);
            double escala = Math.Min((tamano.Width - 2.0 * margen) / original.Width,
                (tamano.Height - 2.0 * margen) / original.Height);
            int ancho = Math.Max(1, (int)Math.Round(original.Width * escala));
            int alto = Math.Max(1, (int)Math.Round(original.Height * escala));
            var destino = new Rectangle((tamano.Width - ancho) / 2, (tamano.Height - alto) / 2, ancho, alto);

            miniatura = new Bitmap(tamano.Width, tamano.Height, PixelFormat.Format32bppArgb);
            using var graficos = Graphics.FromImage(miniatura);
            graficos.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graficos.DrawImage(original, destino);
            return miniatura;
        }
        catch (Exception excepcion) when (excepcion is ArgumentException or ExternalException)
        {
            miniatura?.Dispose();
            return null;
        }
    }

    private sealed record FichaDeSteam(
        [property: JsonPropertyName("success")] bool Disponible,
        [property: JsonPropertyName("data")] DatosDeSteam? Datos);
    private sealed record DatosDeSteam([property: JsonPropertyName("header_image")] string? Portada);
}
