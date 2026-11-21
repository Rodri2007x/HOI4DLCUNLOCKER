using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace DlcDeHoi4;

public partial class FormularioPrincipal : Form
{
    private const int AtributoTemaOscuro = 20;
    private const int AtributoColorDeTitulo = 35;
    private const int AtributoColorDelTextoDeTitulo = 36;
    private const int ColorDeTituloPredeterminado = -1;
    private const int DescargasSimultaneas = 6;
    private static readonly Icon IconoDeLaAplicacion = new(typeof(FormularioPrincipal), "Recursos.PaqueteYLupa.ico");
    private readonly ImageList imagenesDePortadas = new() { ColorDepth = ColorDepth.Depth32Bit };
    private readonly ConfiguracionDeInicio configuracion;
    private CancellationTokenSource? deteccionEnCurso;
    private CancellationTokenSource? portadasEnCurso;
    private CatalogoDeDlc? catalogo;
    private string? carpetaDelJuego;
    private bool buscarCarpeta;

    private static bool TemaOscuroDisponible =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !SystemInformation.HighContrast;

    private bool TemaOscuroActivo => casillaOscuro?.Checked == true && TemaOscuroDisponible;

    public FormularioPrincipal()
        : this(new ConfiguracionDeInicio(null, null))
    {
    }

    internal FormularioPrincipal(ConfiguracionDeInicio configuracion)
    {
        this.configuracion = configuracion;
        carpetaDelJuego = configuracion.CarpetaDelJuego;
        InitializeComponent();
        Icon = IconoDeLaAplicacion;
        casillaOscuro.Enabled = TemaOscuroDisponible;
        if (configuracion.Catalogo is not null)
        {
            listaDeDlc.CheckBoxes = true;
            listaDeDlc.Columns.Add("Estado", 140);
            listaDeDlc.AccessibleName = "Catálogo de DLC";
            grupoDeInstalados.Header = "DLC del catálogo";
            ClientSize = new Size(690, 440);
        }
    }

    protected override void OnHandleCreated(EventArgs argumentos)
    {
        base.OnHandleCreated(argumentos);
        if (TemaOscuroActivo)
        {
            AplicarTemaDeTitulo(oscuro: true);
        }
    }

    protected override async void OnShown(EventArgs argumentos)
    {
        base.OnShown(argumentos);
        await CargarDlcAsync();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            deteccionEnCurso?.Cancel();
            portadasEnCurso?.Cancel();
            descargaEnCurso?.Cancel();
        }

        base.Dispose(disposing);
        if (disposing)
        {
            imagenesDePortadas.Dispose();
        }
    }

    private void CambiarTema(object? remitente, EventArgs argumentos)
    {
        Application.SetColorMode(TemaOscuroActivo ? SystemColorMode.Dark : SystemColorMode.Classic);
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        bool oscuro = TemaOscuroActivo;
        AplicarTemaDeTitulo(oscuro);
        listaDeDlc.AplicarTema(oscuro);
        Invalidate(invalidateChildren: true);
    }

    private void AplicarTemaDeTitulo(bool oscuro)
    {
        int habilitado = oscuro ? 1 : 0;
        int fondo = oscuro ? ColorTranslator.ToWin32(SystemColors.Window) : ColorDeTituloPredeterminado;
        int texto = oscuro ? ColorTranslator.ToWin32(SystemColors.WindowText) : ColorDeTituloPredeterminado;
        _ = DwmSetWindowAttribute(Handle, AtributoTemaOscuro, habilitado, sizeof(int));
        _ = DwmSetWindowAttribute(Handle, AtributoColorDeTitulo, fondo, sizeof(int));
        _ = DwmSetWindowAttribute(Handle, AtributoColorDelTextoDeTitulo, texto, sizeof(int));
    }

    private async void ReintentarDeteccion(object? remitente, EventArgs argumentos)
    {
        if (buscarCarpeta)
        {
            using var dialogo = new FolderBrowserDialog
            {
                Description = "Selecciona la carpeta de Hearts of Iron IV que contiene hoi4.exe",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = false,
            };
            if (dialogo.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            if (!LocalizadorDeHoi4.EsCarpetaDelJuego(dialogo.SelectedPath))
            {
                MostrarMensajeDeReintento("Selecciona la carpeta que contiene hoi4.exe.", buscarJuego: true);
                return;
            }

            carpetaDelJuego = dialogo.SelectedPath;
        }

        await CargarDlcAsync();
    }

    private async Task<InstalacionDeHoi4?> CargarDlcAsync()
    {
        if (deteccionEnCurso is not null)
        {
            return null;
        }

        using var cancelacion = new CancellationTokenSource();
        deteccionEnCurso = cancelacion;
        ActualizarControlesDeDescarga();
        portadasEnCurso?.Cancel();
        botonReintentar.Hide();
        listaDeDlc.Hide();
        panelDeEstado.Show();
        barraDeDescarga.Hide();

        try
        {
            string? carpeta = carpetaDelJuego;
            if (carpeta is null)
            {
                carpeta = await EjecutarEtapaAsync("Buscando Hearts of Iron IV en Steam…",
                    () => LocalizadorDeHoi4.Buscar(cancelacion.Token), cancelacion.Token) ??
                    throw new DirectoryNotFoundException();
            }

            InstalacionDeHoi4 instalacion = await EjecutarEtapaAsync("Leyendo DLC y metadatos…",
                () => InstalacionDeHoi4.Leer(carpeta, cancelacion.Token), cancelacion.Token);
            carpetaDelJuego = instalacion.Carpeta;
            if (catalogo is null && configuracion.Catalogo is Uri direccion)
            {
                etiquetaDeEstado.Text = "Cargando catálogo…";
                catalogo = await CatalogoDeDlc.CargarAsync(direccion, cancelacion.Token);
            }

            if (IsDisposed)
            {
                return null;
            }

            if (instalacion.Detectados.Length == 0 && catalogo?.Dlcs.Length is not > 0)
            {
                MostrarMensajeDeReintento("No se encontraron DLC en la carpeta del juego.");
                return null;
            }

            listaDeDlc.Items.Clear();
            PrepararPortadas();
            List<(DlcDetectado Dlc, ListViewItem Fila)> filas = MostrarLista(instalacion);

            panelDeEstado.Hide();
            listaDeDlc.Show();
            if (catalogo is not null)
            {
                string? version = instalacion.VersionDelJuego is string versionDelJuego
                    ? $"{versionDelJuego} (Steam)"
                    : instalacion.BuildId;
                etiquetaDescarga.Text = instalacion.BuildId is null
                    ? "No se pudo identificar la build de Steam."
                    : instalacion.Metadatos is null
                        ? "Revisa 00_dlc_info.txt antes de descargar."
                        : $"Build de HOI4 {version}";
                barraDeDescarga.Show();
            }

            _ = ActualizarPortadasAsync(filas);
            return instalacion;
        }
        catch (DirectoryNotFoundException)
        {
            MostrarMensajeDeReintento("No se encontró la carpeta de Hearts of Iron IV.", buscarJuego: true);
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            MostrarMensajeDeReintento("No se pudieron leer los archivos de Hearts of Iron IV.");
        }
        catch (Exception excepcion) when (excepcion is HttpRequestException or JsonException or InvalidDataException or OperationCanceledException)
        {
            if (!cancelacion.IsCancellationRequested)
            {
                MostrarMensajeDeReintento($"No se pudo cargar el catálogo. {excepcion.Message}");
            }
        }
        finally
        {
            deteccionEnCurso = null;
            ActualizarControlesDeDescarga();
        }

        return null;
    }

    private async Task ActualizarPortadasAsync(List<(DlcDetectado Dlc, ListViewItem Fila)> filas)
    {
        using var cancelacion = new CancellationTokenSource();
        portadasEnCurso = cancelacion;
        try
        {
            await CargarPortadasAsync(filas, cancelacion.Token);
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(portadasEnCurso, cancelacion))
            {
                portadasEnCurso = null;
            }
        }
    }

    private void PrepararPortadas()
    {
        int ancho = Math.Min(LogicalToDeviceUnits(96), 256);
        int alto = ancho / 2;
        imagenesDePortadas.Images.Clear();
        imagenesDePortadas.ImageSize = new Size(ancho, alto);
        _ = imagenesDePortadas.Handle;

        using var sinPortada = new Bitmap(ancho, alto);
        using (var graficos = Graphics.FromImage(sinPortada))
        {
            int margen = Math.Max(2, alto / 24);
            using var fondo = new SolidBrush(Color.FromArgb(36, Color.Gray));
            using var marca = new SolidBrush(Color.FromArgb(180, Color.Gray));
            graficos.FillRectangle(fondo, margen, margen, ancho - 2 * margen, alto - 2 * margen);
            graficos.FillRectangle(marca, ancho / 2 - 4 * margen, alto / 2 - margen / 2, 8 * margen, margen);
        }

        imagenesDePortadas.Images.Add(sinPortada);
        listaDeDlc.SmallImageList = imagenesDePortadas;
    }

    private async Task CargarPortadasAsync(
        List<(DlcDetectado Dlc, ListViewItem Fila)> filas, CancellationToken cancelacion)
    {
        Size tamano = imagenesDePortadas.ImageSize;
        var pendientesDeTienda = new Dictionary<uint, ListViewItem[]>();
        foreach (var grupo in filas.GroupBy(entrada => entrada.Dlc.IdDeSteam))
        {
            cancelacion.ThrowIfCancellationRequested();
            if (grupo.Key is uint id)
            {
                ListViewItem[] filasDelDlc = grupo.Select(entrada => entrada.Fila).ToArray();
                using Bitmap? guardada = await PortadasDeSteam.LeerCacheAsync(id, tamano, cancelacion);
                if (guardada is not null)
                {
                    MostrarPortada(guardada, filasDelDlc, cancelacion);
                    continue;
                }

                pendientesDeTienda.Add(id, filasDelDlc);
            }

            foreach ((DlcDetectado dlc, ListViewItem fila) in grupo)
            {
                if (dlc.ImagenLocal is string ruta)
                {
                    using Bitmap? miniatura = await PortadasDeSteam.LeerImagenLocalAsync(ruta, tamano, cancelacion);
                    MostrarPortada(miniatura, [fila], cancelacion);
                }
            }
        }

        using var limite = new SemaphoreSlim(DescargasSimultaneas);
        await Task.WhenAll(pendientesDeTienda.Select(async entrada =>
        {
            await limite.WaitAsync(cancelacion);
            try
            {
                using Bitmap? portada = await PortadasDeSteam.DescargarAsync(entrada.Key, tamano, cancelacion);
                MostrarPortada(portada, entrada.Value, cancelacion);
            }
            finally
            {
                limite.Release();
            }
        }));
    }

    private void MostrarPortada(Bitmap? portada, ListViewItem[] filas, CancellationToken cancelacion)
    {
        cancelacion.ThrowIfCancellationRequested();
        if (portada is null || IsDisposed)
        {
            return;
        }

        int indice = imagenesDePortadas.Images.Count;
        imagenesDePortadas.Images.Add(portada);
        foreach (ListViewItem fila in filas)
        {
            fila.ImageIndex = indice;
        }
    }

    private Task<TResultado> EjecutarEtapaAsync<TResultado>(
        string mensaje, Func<TResultado> operacion, CancellationToken cancelacion)
    {
        cancelacion.ThrowIfCancellationRequested();
        etiquetaDeEstado.Text = mensaje;
        panelDeEstado.Refresh();
        return Task.Run(operacion, cancelacion);
    }

    private void MostrarMensajeDeReintento(string mensaje, bool buscarJuego = false)
    {
        if (!IsDisposed)
        {
            etiquetaDeEstado.Text = mensaje;
            buscarCarpeta = buscarJuego;
            botonReintentar.Text = buscarJuego ? "Buscar carpeta" : "Reintentar";
            botonReintentar.Show();
        }
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(nint ventana, int atributo, in int valor, int tamano);
}
