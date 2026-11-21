namespace DlcDeHoi4;

internal sealed record ConfiguracionDeInicio(string? CarpetaDelJuego, Uri? Catalogo)
{
    public static ConfiguracionDeInicio Leer(string[] argumentos)
    {
        string? carpeta = null;
        string? direccion = null;
        for (int indice = 0; indice < argumentos.Length; indice += 2)
        {
            if (indice + 1 == argumentos.Length || string.IsNullOrWhiteSpace(argumentos[indice + 1]))
            {
                throw new ArgumentException($"Falta el valor para {argumentos[indice]}.");
            }

            switch (argumentos[indice])
            {
                case "--catalogo":
                    direccion = argumentos[indice + 1];
                    break;
                case "--juego":
                    carpeta = Path.GetFullPath(argumentos[indice + 1]);
                    break;
                default:
                    throw new ArgumentException($"Opción desconocida: {argumentos[indice]}.");
            }
        }

        string archivo = Path.Combine(AppContext.BaseDirectory, "catalogo.url");
        if (direccion is null && File.Exists(archivo))
        {
            direccion = File.ReadAllText(archivo).Trim();
        }

        return new ConfiguracionDeInicio(carpeta, direccion is null ? null : CatalogoDeDlc.LeerDireccion(direccion));
    }
}
