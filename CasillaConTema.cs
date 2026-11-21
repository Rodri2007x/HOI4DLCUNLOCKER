using System.Windows.Forms.VisualStyles;

namespace DlcDeHoi4;

internal sealed class CasillaConTema : CheckBox
{
    private bool oscuro;

    public void AplicarTema(bool temaOscuro)
    {
        oscuro = temaOscuro;
        if (oscuro)
        {
            BackColor = Color.FromArgb(SystemColors.Window.ToArgb());
            ForeColor = Color.FromArgb(250, 250, 250);
        }
        else
        {
            ResetBackColor();
            ResetForeColor();
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!oscuro || Enabled)
        {
            base.OnPaint(e);
            return;
        }

        using var fondo = new SolidBrush(BackColor);
        e.Graphics.FillRectangle(fondo, e.ClipRectangle);

        CheckBoxState estado = CheckState switch
        {
            CheckState.Checked => CheckBoxState.CheckedDisabled,
            CheckState.Indeterminate => CheckBoxState.MixedDisabled,
            _ => CheckBoxState.UncheckedDisabled,
        };
        Size tamano = CheckBoxRenderer.GetGlyphSize(e.Graphics, estado);
        var posicion = new Point(0, (ClientSize.Height - tamano.Height) / 2);
        CheckBoxRenderer.DrawCheckBox(e.Graphics, posicion, estado);

        int izquierda = Math.Min(ClientSize.Width, tamano.Width + 3 * DeviceDpi / 96);
        var texto = Rectangle.FromLTRB(izquierda, 0, ClientSize.Width, ClientSize.Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, texto, Color.Gainsboro,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.HidePrefix | TextFormatFlags.EndEllipsis);
    }
}
