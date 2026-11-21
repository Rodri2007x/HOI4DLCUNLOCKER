using System.Globalization;

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
        if (catalogo is not CatalogoDeDlc catalogoActivo || instalacionMostrada is not InstalacionDeHoi4 instalacion ||
            descargaEnCurso is not null || deteccionEnCurso is not null)
        {
            return;
        }

        FichaDeDlc[] fichas = listaDeDlc.CheckedItems.Cast<ListViewItem>().Select(fila => fila.Tag).OfType<FichaDeDlc>().ToArray();
        if (fichas.Length == 0)
        {
            return;
        }

        string confirmacion = $"¿Confirmas la descarga e instalación de {fichas.Length} DLC en esta carpeta?\n\n" +
            $"{instalacion.Carpeta}\n\nLa instalación completada es irreversible desde la app.";
        if (MessageBox.Show(this, confirmacion, "Confirmar descarga e instalación",
                MessageBoxButtons.OKCancel, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) != DialogResult.OK)
        {
            return;
        }

        bool incluirSoundtracks = casillaSoundtracks.Checked;
        uint[] ids = fichas.Select(ficha => ficha.AppId).ToArray();
        using var cancelacion = new CancellationTokenSource();
        descargaEnCurso = cancelacion;
        ActualizarControlesDeDescarga();

        try
        {
            using var dialogo = new FormularioDeInstalacion(cancelacion, (progreso, token) =>
                InstaladorDeDlc.InstalarAsync(instalacion.Carpeta, catalogoActivo, ids, progreso, token, incluirSoundtracks));
            dialogo.ShowDialog(this);
            if (dialogo.InstalacionCompletada && !IsDisposed && !cerrarTrasDescarga)
            {
                await CargarDlcAsync();
            }
        }
        finally
        {
            descargaEnCurso = null;
            if (!IsDisposed)
            {
                ActualizarControlesDeDescarga();
                if (cerrarTrasDescarga)
                {
                    Close();
                }
            }
        }
    }

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
