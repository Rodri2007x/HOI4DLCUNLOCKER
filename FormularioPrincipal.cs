using System.Globalization;

namespace DlcDeHoi4;

public partial class FormularioPrincipal : Form
{
    private static readonly Icon IconoDeLaAplicacion = new(typeof(FormularioPrincipal), "Recursos.PaqueteYLupa.ico");
    private CancellationTokenSource? deteccionEnCurso;

    public FormularioPrincipal()
    {
        InitializeComponent();
        Icon = IconoDeLaAplicacion;
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
}
