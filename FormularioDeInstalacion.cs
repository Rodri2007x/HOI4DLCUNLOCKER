using System.Text;

namespace DlcDeHoi4;

internal sealed class FormularioDeInstalacion : Form
{
    private readonly CancellationTokenSource cancelacion;
    private readonly Func<IProgress<ProgresoDeDescarga>, CancellationToken, Task> instalar;
    private readonly Label actividad = new() { AutoSize = true, Text = "Preparando la instalación…" };
    private readonly Label detalle = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly ProgressBar progreso = new() { Dock = DockStyle.Fill, AccessibleName = "Progreso de instalación" };
    private readonly Label porcentaje = new() { AutoSize = true, Anchor = AnchorStyles.Right, Text = "0 %" };
    private readonly BotonConTema accion = new() { AutoSize = true, Anchor = AnchorStyles.Right, Text = "Cancelar" };
    private bool aplicando;
    private bool finalizada;

    public bool InstalacionCompletada { get; private set; }

    public FormularioDeInstalacion(CancellationTokenSource cancelacion,
        Func<IProgress<ProgresoDeDescarga>, CancellationToken, Task> instalar, bool temaOscuro)
    {
        this.cancelacion = cancelacion;
        this.instalar = instalar;

        Text = "Descarga e instalación de DLC";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(548, 198);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;

        var contenido = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 4,
        };
        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        contenido.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contenido.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        contenido.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        actividad.MaximumSize = new Size(500, 0);
        detalle.Margin = new Padding(3, 6, 3, 8);
        progreso.Height = 16;
        porcentaje.MinimumSize = new Size(44, 0);
        porcentaje.TextAlign = ContentAlignment.MiddleRight;
        accion.MinimumSize = new Size(88, 0);
        accion.Margin = new Padding(3, 12, 3, 0);
        accion.AplicarTema(temaOscuro);
        accion.Click += (_, _) => EjecutarAccion();

        contenido.Controls.Add(actividad, 0, 0);
        contenido.SetColumnSpan(actividad, 2);
        contenido.Controls.Add(detalle, 0, 1);
        contenido.SetColumnSpan(detalle, 2);
        contenido.Controls.Add(progreso, 0, 2);
        contenido.Controls.Add(porcentaje, 1, 2);
        contenido.Controls.Add(accion, 1, 3);
        Controls.Add(contenido);
    }

    protected override async void OnShown(EventArgs argumentos)
    {
        base.OnShown(argumentos);
        var avance = new Progress<ProgresoDeDescarga>(MostrarAvance);
        try
        {
            await Task.Run(() => instalar(avance, cancelacion.Token), cancelacion.Token);
            InstalacionCompletada = true;
            Finalizar("Instalación completada.");
        }
        catch (OperationCanceledException)
        {
            Finalizar(cancelacion.IsCancellationRequested ? "Instalación cancelada." : "La descarga tardó demasiado.");
        }
        catch (Exception error) when (error is HttpRequestException or IOException or InvalidDataException or
            UnauthorizedAccessException or DecoderFallbackException or OverflowException)
        {
            Finalizar("No se pudo completar la instalación.", error.Message);
        }
    }

    private void MostrarAvance(ProgresoDeDescarga avance)
    {
        if (finalizada || IsDisposed)
        {
            return;
        }

        aplicando |= avance.Aplicando;
        MostrarPorcentaje(Math.Max(progreso.Value, avance.Porcentaje));
        if (!cancelacion.IsCancellationRequested || aplicando)
        {
            actividad.Text = avance.Mensaje;
            detalle.Text = avance.Archivo ?? string.Empty;
        }

        accion.Enabled = !aplicando && !cancelacion.IsCancellationRequested;
    }

    private void MostrarPorcentaje(int valor)
    {
        progreso.Value = Math.Clamp(valor, 0, 100);
        porcentaje.Text = $"{progreso.Value} %";
    }

    private void Finalizar(string mensaje, string? explicacion = null)
    {
        if (IsDisposed)
        {
            return;
        }

        finalizada = true;
        actividad.Text = mensaje;
        detalle.Text = explicacion ?? string.Empty;
        if (InstalacionCompletada)
        {
            MostrarPorcentaje(100);
        }

        accion.Text = "Cerrar";
        accion.Enabled = true;
        AcceptButton = accion;
        accion.Focus();
    }

    private void EjecutarAccion()
    {
        if (finalizada)
        {
            Close();
        }
        else if (!aplicando && !cancelacion.IsCancellationRequested)
        {
            accion.Enabled = false;
            actividad.Text = "Cancelando la instalación…";
            detalle.Text = string.Empty;
            cancelacion.Cancel();
        }
    }

    protected override bool ProcessDialogKey(Keys tecla)
    {
        if (tecla == Keys.Escape)
        {
            EjecutarAccion();
            return true;
        }

        return base.ProcessDialogKey(tecla);
    }

    protected override void OnFormClosing(FormClosingEventArgs argumentos)
    {
        if (argumentos.CloseReason == CloseReason.UserClosing && !finalizada)
        {
            argumentos.Cancel = true;
            EjecutarAccion();
        }

        base.OnFormClosing(argumentos);
    }
}
