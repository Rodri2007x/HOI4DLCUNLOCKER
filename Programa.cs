namespace DlcDeHoi4;

internal static class Programa
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new FormularioPrincipal());
    }
}
