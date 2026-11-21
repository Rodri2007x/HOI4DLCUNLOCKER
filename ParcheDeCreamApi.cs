using System.Reflection;
using System.Text;

namespace DlcDeHoi4;

internal sealed class ParcheDeCreamApi
{
    private const uint AppIdDeHoi4 = 394360;
    private static readonly (string Nombre, string Original)[] Apis =
    [
        ("steam_api.dll", "steam_api_o.dll"),
        ("steam_api64.dll", "steam_api64_o.dll"),
    ];

    private readonly List<Cambio> cambios = [];

    private ParcheDeCreamApi()
    {
    }

    public int Operaciones => cambios.Count;

    public static void Validar(string raiz)
    {
        bool encontrado = false;
        foreach ((string nombre, string original) in Apis)
        {
            string destino = Path.Combine(raiz, nombre);
            string respaldo = Path.Combine(raiz, original);
            InstaladorDeDlc.ValidarRuta(destino);
            InstaladorDeDlc.ValidarRuta(respaldo);
            encontrado |= File.Exists(destino) || File.Exists(respaldo);
        }

        InstaladorDeDlc.ValidarRuta(Path.Combine(raiz, "cream_api.ini"));
        if (!encontrado)
        {
            throw new InvalidDataException("No se encontró la DLL de Steamworks necesaria para aplicar CreamAPI.");
        }
    }

    public static ParcheDeCreamApi Preparar(string raiz, string temporal, IEnumerable<FichaDeDlc> dlcs)
    {
        Validar(raiz);
        var parche = new ParcheDeCreamApi();
        string carpeta = Directory.CreateDirectory(Path.Combine(temporal, "creamapi")).FullName;
        foreach ((string nombre, string original) in Apis)
        {
            string destino = Path.Combine(raiz, nombre);
            string respaldoOriginal = Path.Combine(raiz, original);
            if (!File.Exists(destino) && !File.Exists(respaldoOriginal))
            {
                continue;
            }

            string origen = Path.Combine(carpeta, nombre);
            ExtraerRecurso(nombre, origen);
            parche.cambios.Add(new Cambio(origen, destino, Path.Combine(carpeta, $"respaldo-{parche.cambios.Count}"),
                respaldoOriginal));
        }

        string configuracion = Path.Combine(carpeta, "cream_api.ini");
        File.WriteAllText(configuracion, GenerarConfiguracion(dlcs), new UTF8Encoding(false));
        parche.cambios.Add(new Cambio(configuracion, Path.Combine(raiz, "cream_api.ini"),
            Path.Combine(carpeta, "respaldo-configuracion"), null));
        return parche;
    }

    public void Aplicar(Action<string> informar)
    {
        foreach (Cambio cambio in cambios)
        {
            informar(cambio.Destino);
            if (File.Exists(cambio.Destino))
            {
                File.Move(cambio.Destino, cambio.Respaldo);
            }

            if (cambio.Original is not null && !File.Exists(cambio.Original))
            {
                if (!File.Exists(cambio.Respaldo))
                {
                    throw new InvalidDataException($"La DLL original de {Path.GetFileName(cambio.Destino)} desapareció durante la instalación.");
                }

                File.Move(cambio.Respaldo, cambio.Original);
                cambio.OriginalCreado = true;
            }

            File.Move(cambio.Origen, cambio.Destino);
            cambio.Reemplazado = true;
        }
    }

    public void Deshacer()
    {
        for (int indice = cambios.Count - 1; indice >= 0; indice--)
        {
            Cambio cambio = cambios[indice];
            if (cambio.Reemplazado && File.Exists(cambio.Destino))
            {
                File.Delete(cambio.Destino);
            }

            if (cambio.OriginalCreado)
            {
                File.Move(cambio.Original!, cambio.Destino);
            }

            if (File.Exists(cambio.Respaldo))
            {
                File.Move(cambio.Respaldo, cambio.Destino);
            }
        }
    }

    private static void ExtraerRecurso(string nombre, string destino)
    {
        using Stream recurso = Assembly.GetExecutingAssembly().GetManifestResourceStream($"DlcDeHoi4.Recursos.CreamAPI.{nombre}") ??
            throw new InvalidDataException($"Falta el recurso integrado de CreamAPI: {nombre}");
        using var salida = new FileStream(destino, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        recurso.CopyTo(salida);
    }

    private static string GenerarConfiguracion(IEnumerable<FichaDeDlc> dlcs)
    {
        var texto = new StringBuilder();
        texto.AppendLine("; Hearts of Iron IV");
        texto.AppendLine("[steam]");
        texto.AppendLine($"appid = {AppIdDeHoi4}");
        texto.AppendLine("unlockall = false");
        texto.AppendLine("orgapi = steam_api_o.dll");
        texto.AppendLine("orgapi64 = steam_api64_o.dll");
        texto.AppendLine("extraprotection = false");
        texto.AppendLine("forceoffline = false");
        texto.AppendLine();
        texto.AppendLine("[steam_misc]");
        texto.AppendLine("disableuserinterface = false");
        texto.AppendLine();
        texto.AppendLine("[dlc]");
        foreach (FichaDeDlc dlc in dlcs.OrderBy(dlc => dlc.AppId))
        {
            texto.Append(dlc.AppId).Append(" = ").AppendLine(dlc.Nombre.ReplaceLineEndings(" "));
        }

        return texto.ToString();
    }

    private sealed class Cambio(string origen, string destino, string respaldo, string? original)
    {
        public string Origen { get; } = origen;
        public string Destino { get; } = destino;
        public string Respaldo { get; } = respaldo;
        public string? Original { get; } = original;
        public bool OriginalCreado { get; set; }
        public bool Reemplazado { get; set; }
    }
}
