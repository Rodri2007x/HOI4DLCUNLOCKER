using System.Globalization;
using System.Runtime.InteropServices;

namespace DlcDeHoi4;

public partial class FormularioPrincipal : Form
{
    private const int AtributoTemaOscuro = 20;
    private const int AtributoColorDeTitulo = 35;
    private const int AtributoColorDelTextoDeTitulo = 36;
    private static readonly Icon IconoDeLaAplicacion = new(typeof(FormularioPrincipal), "Recursos.PaqueteYLupa.ico");
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
            const int habilitado = 1;
            int fondo = ColorTranslator.ToWin32(SystemColors.Window);
            int texto = ColorTranslator.ToWin32(SystemColors.WindowText);
            _ = DwmSetWindowAttribute(Handle, AtributoTemaOscuro, habilitado, sizeof(int));
            _ = DwmSetWindowAttribute(Handle, AtributoColorDeTitulo, fondo, sizeof(int));
            _ = DwmSetWindowAttribute(Handle, AtributoColorDelTextoDeTitulo, texto, sizeof(int));
        }
    }

    protected override async void OnShown(EventArgs argumentos)
    {
        base.OnShown(argumentos);
        await CargarDlcAsync();
    }

    protected override void OnFormClosed(FormClosedEventArgs argumentos)
    {
        deteccionEnCurso?.Cancel();
        base.OnFormClosed(argumentos);
    }

    private void CambiarTema(object? remitente, EventArgs argumentos)
    {
        Application.SetColorMode(TemaOscuroActivo ? SystemColorMode.Dark : SystemColorMode.Classic);
        if (IsDisposed)
        {
            return;
        }

        listaDeDlc.BackColor = Color.FromArgb(SystemColors.Window.ToArgb());
        listaDeDlc.ForeColor = Color.FromArgb(SystemColors.WindowText.ToArgb());
        // Al crear sus identificadores nativos, WinForms aplica el tema a los controles.
        RecreateHandle();
        BeginInvoke(AplicarTemaDeLista);
        Refresh();
    }

    private void AplicarTemaDeLista()
    {
        if (!IsDisposed && listaDeDlc.Visible)
        {
            // ItemsView aplica el color oscuro también al texto de los grupos.
            _ = SetWindowTheme(listaDeDlc.Handle, TemaOscuroActivo ? "DarkMode_ItemsView" : null, null);
            listaDeDlc.Refresh();
        }
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

            listaDeDlc.Items.Clear();
            foreach (DlcDetectado dlc in dlcDetectados)
            {
                var fila = new ListViewItem(dlc.Nombre)
                {
                    Group = dlc.EstaIntegrado ? grupoDeIntegrados : grupoDeInstalados,
                };
                fila.SubItems.Add(dlc.IdDeSteam?.ToString(CultureInfo.InvariantCulture) ?? "—");
                listaDeDlc.Items.Add(fila);
            }

            panelDeEstado.Hide();
            listaDeDlc.Show();
            if (TemaOscuroActivo)
            {
                BeginInvoke(AplicarTemaDeLista);
            }
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

    [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(nint ventana, string? tema, string? listaDeClases);
}
