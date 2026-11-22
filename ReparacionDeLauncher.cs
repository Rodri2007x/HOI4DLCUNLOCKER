namespace DlcDeHoi4;

internal sealed class ReparacionDeLauncher
{
    private static readonly string[] Archivos =
    [
        "launcher-v2.sqlite",
        "launcher-v2.sqlite-journal",
        "launcher-v2.sqlite-wal",
        "launcher-v2.sqlite-shm",
    ];

    private readonly string carpeta;
    private readonly string respaldo;
    private readonly List<(string Origen, string Destino)> movimientos = [];

    private ReparacionDeLauncher(string carpeta, string respaldo)
    {
        this.carpeta = carpeta;
        this.respaldo = respaldo;
    }

    public static ReparacionDeLauncher? Preparar(string? carpetaDocumentos = null)
    {
        string carpeta = Path.Combine(carpetaDocumentos ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Paradox Interactive", "Hearts of Iron IV");
        if (!File.Exists(Path.Combine(carpeta, Archivos[0])))
        {
            return null;
        }

        foreach (string nombre in Archivos)
        {
            string ruta = Path.Combine(carpeta, nombre);
            InstaladorDeDlc.ValidarRuta(ruta);
            try
            {
                using var archivo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (FileNotFoundException)
            {
                // Los archivos auxiliares de SQLite son opcionales.
            }
            catch (DirectoryNotFoundException)
            {
                throw new InvalidDataException("La carpeta de datos del launcher desapareció durante la comprobación.");
            }
            catch (IOException error)
            {
                throw new IOException("Cierra Paradox Launcher antes de reparar su base de datos.", error);
            }
        }

        return new ReparacionDeLauncher(carpeta, Path.Combine(carpeta, $".dlcdehoi4-launcher-{Guid.NewGuid():N}"));
    }

    public void Aplicar(Action<string> informar)
    {
        informar(Path.Combine(carpeta, Archivos[0]));
        Directory.CreateDirectory(respaldo);
        foreach (string nombre in Archivos)
        {
            string origen = Path.Combine(carpeta, nombre);
            if (!File.Exists(origen))
            {
                continue;
            }

            string destino = Path.Combine(respaldo, nombre);
            File.Move(origen, destino);
            movimientos.Add((origen, destino));
        }
    }

    public void Deshacer()
    {
        for (int indice = movimientos.Count - 1; indice >= 0; indice--)
        {
            (string origen, string destino) = movimientos[indice];
            File.Move(destino, origen);
        }

        if (Directory.Exists(respaldo) && !Directory.EnumerateFileSystemEntries(respaldo).Any())
        {
            Directory.Delete(respaldo);
        }
    }
}
