using System.Globalization;
using System.Text;

namespace DlcDeHoi4;

public partial class FormularioPrincipal
{
    private CancellationTokenSource? descargaEnCurso;
    private bool cerrarTrasDescarga;

    private List<(DlcDetectado Dlc, ListViewItem Fila)> MostrarLista(InstalacionDeHoi4 instalacion)
    {
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
                string estado = instalacion.EstaInstalado(ficha) ? "✓ Instalado" : "X Falta";
                if (estado == "X Falta" && ficha.Paquete is PaqueteDeDlc paquete && instalacion.BuildId != paquete.BuildId)
                {
                    estado = "X Otra build";
                }

                filas.Add((dlc, CrearFila(dlc, estado, instalacion.SePuedeInstalar(ficha) ? ficha : null)));
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

    private ListViewItem CrearFila(DlcDetectado dlc, string estado, FichaDeDlc? descargable)
    {
        var fila = new ListViewItem(dlc.Nombre)
        {
            Group = dlc.EstaIntegrado ? grupoDeIntegrados : grupoDeInstalados,
            ImageIndex = 0,
            Tag = descargable,
        };
        fila.SubItems.Add(dlc.IdDeSteam?.ToString(CultureInfo.InvariantCulture) ?? "—");
        if (catalogo is not null)
        {
            fila.SubItems.Add(estado);
        }

        return fila;
    }

    private void ComprobarSeleccionDeDescarga(object? remitente, ItemCheckEventArgs argumentos)
    {
        if (descargaEnCurso is not null ||
            argumentos.NewValue == CheckState.Checked && listaDeDlc.Items[argumentos.Index].Tag is not FichaDeDlc)
        {
            argumentos.NewValue = argumentos.CurrentValue;
        }
    }

    private void CambiarSeleccionDeDescarga(object? remitente, ItemCheckedEventArgs argumentos) => ActualizarBotonDescargar();

    private void ActualizarBotonDescargar()
    {
        if (!IsDisposed)
        {
            botonDescargar.Enabled = catalogo is not null && descargaEnCurso is null && deteccionEnCurso is null &&
                listaDeDlc.CheckedItems.Count > 0;
        }
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

        using var cancelacion = new CancellationTokenSource();
        descargaEnCurso = cancelacion;
        ActualizarBotonDescargar();
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
                progreso, cancelacion.Token), cancelacion.Token);
            if (!IsDisposed && !cerrarTrasDescarga && await CargarDlcAsync() is InstalacionDeHoi4 estado)
            {
                etiquetaDescarga.Text = fichas.All(estado.EstaInstalado)
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
                ActualizarBotonDescargar();
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
