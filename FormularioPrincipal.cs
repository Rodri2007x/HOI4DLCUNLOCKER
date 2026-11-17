using System.Globalization;
using System.Runtime.InteropServices;

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
    private CancellationTokenSource? deteccionEnCurso;

    private static bool TemaOscuroDisponible =>
        OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000) && !SystemInformation.HighContrast;

    private bool TemaOscuroActivo => casillaOscuro?.Checked == true && TemaOscuroDisponible;

    public FormularioPrincipal()
    {
        InitializeComponent();
        Icon = IconoDeLaAplicacion;
        casillaOscuro.Enabled = TemaOscuroDisponible;
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
        await CargarDlcAsync();
    }

    private async Task CargarDlcAsync()
    {
        if (deteccionEnCurso is not null)
        {
            return;
        }

        using var cancelacion = new CancellationTokenSource();
        deteccionEnCurso = cancelacion;
        botonReintentar.Hide();
        listaDeDlc.Hide();
        panelDeEstado.Show();

        try
        {
            DirectoryInfo instalacion = await EjecutarEtapaAsync("Verificando instalación…",
                () => DetectorDeDlc.VerificarInstalacion(DetectorDeDlc.CarpetaDelJuego), cancelacion.Token);
            ArchivosDeDlc archivos = await EjecutarEtapaAsync("Leyendo DLC…",
                () => DetectorDeDlc.BuscarArchivos(instalacion, cancelacion.Token), cancelacion.Token);
            DlcDetectado[] dlcDetectados = await EjecutarEtapaAsync("Leyendo metadatos…",
                () => DetectorDeDlc.LeerMetadatos(archivos, cancelacion.Token), cancelacion.Token);
            if (IsDisposed)
            {
                return;
            }

            if (dlcDetectados.Length == 0)
            {
                MostrarMensajeDeReintento("No se encontraron DLC en la carpeta del juego.");
                return;
            }

            PrepararPortadas();
            var filas = new List<(DlcDetectado Dlc, ListViewItem Fila)>(dlcDetectados.Length);
            listaDeDlc.Items.Clear();
            foreach (DlcDetectado dlc in dlcDetectados)
            {
                var fila = new ListViewItem(dlc.Nombre)
                {
                    Group = dlc.EstaIntegrado ? grupoDeIntegrados : grupoDeInstalados,
                    ImageIndex = 0,
                };
                fila.SubItems.Add(dlc.IdDeSteam?.ToString(CultureInfo.InvariantCulture) ?? "—");
                listaDeDlc.Items.Add(fila);
                filas.Add((dlc, fila));
            }

            panelDeEstado.Hide();
            listaDeDlc.Show();
            await CargarPortadasAsync(filas, cancelacion.Token);
        }
        catch (DirectoryNotFoundException)
        {
            MostrarMensajeDeReintento("No se encontró la carpeta de Hearts of Iron IV.");
        }
        catch (Exception excepcion) when (excepcion is IOException or UnauthorizedAccessException)
        {
            MostrarMensajeDeReintento("No se pudieron leer los archivos de Hearts of Iron IV.");
        }
        catch (OperationCanceledException) when (cancelacion.IsCancellationRequested)
        {
        }
        finally
        {
            deteccionEnCurso = null;
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

    private void MostrarMensajeDeReintento(string mensaje)
    {
        if (!IsDisposed)
        {
            etiquetaDeEstado.Text = mensaje;
            botonReintentar.Show();
        }
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(nint ventana, int atributo, in int valor, int tamano);
}
