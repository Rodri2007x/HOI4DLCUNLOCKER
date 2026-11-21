using System.Globalization;
using System.Text;

namespace DlcDeHoi4;

public partial class FormularioPrincipal
{
    private CancellationTokenSource? descargaEnCurso;
    private InstalacionDeHoi4? instalacionMostrada;
    private bool actualizandoSeleccion;
    private bool cerrarTrasDescarga;

    private List<(DlcDetectado Dlc, ListViewItem Fila)> MostrarLista(InstalacionDeHoi4 instalacion)
    {
        instalacionMostrada = instalacion;
        casillaSeleccionarTodos.Visible = catalogo is not null;
        casillaSoundtracks.Visible = catalogo?.Dlcs.Any(ficha => ficha.Paquete?.Soundtrack is not null) == true;
        var filas = new List<(DlcDetectado Dlc, ListViewItem Fila)>();
        var idsDeCatalogo = new HashSet<uint>();
        if (catalogo is not null)
        {
            var detectados = instalacion.Detectados.ToLookup(dlc => dlc.IdDeSteam);
            foreach (FichaDeDlc ficha in catalogo.Dlcs.OrderBy(dlc => dlc.Nombre, StringComparer.CurrentCultureIgnoreCase))
            {
                idsDeCatalogo.Add(ficha.AppId);
                DlcDetectado? local = detectados[ficha.AppId].FirstOrDefault(dlc => dlc.EstaIntegrado) ??
                    detectados[ficha.AppId].FirstOrDefault();
                var dlc = new DlcDetectado(ficha.Nombre, ficha.AppId, local?.EstaIntegrado == true, local?.ImagenLocal);
                filas.Add((dlc, CrearFila(dlc, MostrarEstado(instalacion, ficha), ficha)));
            }
        }

        foreach (DlcDetectado dlc in instalacion.Detectados)
        {
            if (dlc.IdDeSteam is not uint id || !idsDeCatalogo.Contains(id))
            {
                filas.Add((dlc, CrearFila(dlc, "✓ Instalado", null)));
            }
        }

        listaDeDlc.Items.AddRange(filas.Select(entrada => entrada.Fila).ToArray());
        return filas;
    }

    private string MostrarEstado(InstalacionDeHoi4 instalacion, FichaDeDlc ficha)
    {
        if (instalacion.EstaInstalado(ficha, casillaSoundtracks.Checked))
        {
            return "✓ Instalado";
        }

        if (ficha.Paquete is PaqueteDeDlc paquete && instalacion.BuildId != paquete.BuildId)
        {
            return "X Otra build";
        }

        return casillaSoundtracks.Checked && instalacion.EstaInstalado(ficha) ? "X Falta soundtrack" : "X Falta";
    }

    private ListViewItem CrearFila(DlcDetectado dlc, string estado, FichaDeDlc? ficha)
    {
        var fila = new ListViewItem(dlc.Nombre)
        {
            Group = dlc.EstaIntegrado ? grupoDeIntegrados : grupoDeInstalados,
            ImageIndex = 0,
            Tag = ficha,
        };
        fila.SubItems.Add(dlc.IdDeSteam?.ToString(CultureInfo.InvariantCulture) ?? "—");
        if (catalogo is not null)
        {
            fila.SubItems.Add(estado);
        }

        return fila;
    }

    private bool SePuedeSeleccionar(ListViewItem fila) =>
        fila.Tag is FichaDeDlc ficha && instalacionMostrada?.SePuedeInstalar(ficha, casillaSoundtracks.Checked) == true;

    private void ComprobarSeleccionDeDescarga(object? remitente, ItemCheckEventArgs argumentos)
    {
        if (descargaEnCurso is not null ||
            argumentos.NewValue == CheckState.Checked && !SePuedeSeleccionar(listaDeDlc.Items[argumentos.Index]))
        {
            argumentos.NewValue = argumentos.CurrentValue;
        }
    }

    private void CambiarSeleccionDeDescarga(object? remitente, ItemCheckedEventArgs argumentos) => ActualizarControlesDeDescarga();

    private void CambiarSeleccionDeTodos(object? remitente, EventArgs argumentos)
    {
        if (!casillaSeleccionarTodos.Enabled)
        {
            return;
        }

        bool marcar = casillaSeleccionarTodos.CheckState != CheckState.Checked;
        actualizandoSeleccion = true;
        listaDeDlc.BeginUpdate();
        try
        {
            foreach (ListViewItem fila in listaDeDlc.Items)
            {
                fila.Checked = marcar && SePuedeSeleccionar(fila);
            }
        }
        finally
        {
            listaDeDlc.EndUpdate();
            actualizandoSeleccion = false;
        }

        ActualizarControlesDeDescarga();
    }

    private void CambiarInclusionDeSoundtracks(object? remitente, EventArgs argumentos)
    {
        if (instalacionMostrada is not InstalacionDeHoi4 instalacion)
        {
            return;
        }

        foreach (ListViewItem fila in listaDeDlc.Items)
        {
            if (fila.Tag is FichaDeDlc ficha)
            {
                fila.SubItems[2].Text = MostrarEstado(instalacion, ficha);
                if (fila.Checked && !SePuedeSeleccionar(fila))
                {
                    fila.Checked = false;
                }
            }
        }

        ActualizarControlesDeDescarga();
    }

    private void ActualizarControlesDeDescarga()
    {
        if (IsDisposed || actualizandoSeleccion)
        {
            return;
        }

        bool disponible = catalogo is not null && descargaEnCurso is null && deteccionEnCurso is null;
        casillaSoundtracks.Enabled = disponible;
        if (!disponible)
        {
            botonDescargar.Enabled = false;
            casillaSeleccionarTodos.Enabled = false;
            return;
        }

        var seleccionables = listaDeDlc.Items.Cast<ListViewItem>().Where(SePuedeSeleccionar).ToArray();
        int marcados = seleccionables.Count(fila => fila.Checked);
        botonDescargar.Enabled = marcados > 0;
        casillaSeleccionarTodos.Enabled = seleccionables.Length > 0;
        casillaSeleccionarTodos.CheckState = marcados == 0 ? CheckState.Unchecked :
            marcados == seleccionables.Length ? CheckState.Checked : CheckState.Indeterminate;
    }

    private async void DescargarSeleccionados(object? remitente, EventArgs argumentos)
    {
        if (catalogo is null || descargaEnCurso is not null || deteccionEnCurso is not null)
        {
            return;
        }

        FichaDeDlc[] fichas = listaDeDlc.CheckedItems.Cast<ListViewItem>().Select(fila => fila.Tag).OfType<FichaDeDlc>().ToArray();
        if (fichas.Length == 0)
        {
            return;
        }

        bool incluirSoundtracks = casillaSoundtracks.Checked;
        using var cancelacion = new CancellationTokenSource();
        descargaEnCurso = cancelacion;
        ActualizarControlesDeDescarga();
        botonCancelarDescarga.Show();
        progresoDescarga.Value = 0;
        progresoDescarga.Show();
        etiquetaDescarga.Text = "Preparando descarga…";

        var progreso = new Progress<ProgresoDeDescarga>(actual =>
        {
            if (!IsDisposed && ReferenceEquals(descargaEnCurso, cancelacion))
            {
                etiquetaDescarga.Text = actual.Mensaje;
                progresoDescarga.Value = (int)Math.Clamp(actual.Descargados * 100.0 / actual.Total, 0, 100);
            }
        });

        try
        {
            uint[] ids = fichas.Select(ficha => ficha.AppId).ToArray();
            await Task.Run(() => InstaladorDeDlc.InstalarAsync(configuracion.CarpetaDelJuego, catalogo, ids,
                progreso, cancelacion.Token, incluirSoundtracks), cancelacion.Token);
            if (!IsDisposed && !cerrarTrasDescarga && await CargarDlcAsync() is InstalacionDeHoi4 estado)
            {
                etiquetaDescarga.Text = fichas.All(ficha => estado.EstaInstalado(ficha, incluirSoundtracks))
                    ? "Instalación completada."
                    : "Revisa el estado de los DLC seleccionados.";
            }
        }
        catch (OperationCanceledException)
        {
            if (!IsDisposed)
            {
                etiquetaDescarga.Text = cancelacion.IsCancellationRequested ? "Descarga cancelada." : "La descarga tardó demasiado.";
            }
        }
        catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException or
            UnauthorizedAccessException or DecoderFallbackException or OverflowException)
        {
            if (!IsDisposed && !cerrarTrasDescarga)
            {
                etiquetaDescarga.Text = "No se pudo completar la instalación.";
                MessageBox.Show(this, error.Message, "Descarga de DLC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        finally
        {
            descargaEnCurso = null;
            if (!IsDisposed)
            {
                botonCancelarDescarga.Hide();
                progresoDescarga.Hide();
                ActualizarControlesDeDescarga();
                if (cerrarTrasDescarga)
                {
                    Close();
                }
            }
        }
    }

    private void CancelarDescarga(object? remitente, EventArgs argumentos) => descargaEnCurso?.Cancel();

    protected override void OnFormClosing(FormClosingEventArgs argumentos)
    {
        if (argumentos.CloseReason == CloseReason.UserClosing && descargaEnCurso is not null)
        {
            argumentos.Cancel = true;
            cerrarTrasDescarga = true;
            descargaEnCurso.Cancel();
        }

        base.OnFormClosing(argumentos);
    }
}
