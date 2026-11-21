namespace DlcDeHoi4;

internal sealed class BotonConTema : Button
{
    public void AplicarTema(bool oscuro)
    {
        if (oscuro)
        {
            BackColor = Color.FromArgb(45, 45, 45);
            ForeColor = Color.FromArgb(250, 250, 250);
            UseVisualStyleBackColor = false;
        }
        else
        {
            ResetBackColor();
            ResetForeColor();
        }

        if (IsHandleCreated)
        {
            RecreateHandle();
        }
    }
}
