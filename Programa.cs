namespace DlcDeHoi4;

internal static class Programa
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        ConfiguracionDeInicio configuracion;
        try
        {
            configuracion = ConfiguracionDeInicio.Leer(args);
        }
        catch (Exception error) when (error is ArgumentException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            MessageBox.Show(error.Message, "Configuración de Unlocker", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new FormularioPrincipal(configuracion));
    }
}
