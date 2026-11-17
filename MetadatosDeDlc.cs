using System.Globalization;
using System.Text;

namespace DlcDeHoi4;

internal sealed record EntradaDeMetadatos(string Clave, uint? AppId, int Inicio, string Bloque);

internal sealed class MetadatosDeDlc
{
    public const int MaximoDeBytes = 1024 * 1024;
    private readonly Dictionary<string, EntradaDeMetadatos> entradas;
    private readonly int cierre;
    public string Texto { get; }
    public Encoding Codificacion { get; }

    private MetadatosDeDlc(string texto, Encoding codificacion, Dictionary<string, EntradaDeMetadatos> entradas, int cierre)
    {
        Texto = texto;
        Codificacion = codificacion;
        this.entradas = entradas;
        this.cierre = cierre;
    }

    public static MetadatosDeDlc Leer(string ruta)
    {
        using var flujo = File.OpenRead(ruta);
        if (flujo.Length > MaximoDeBytes)
        {
            throw new InvalidDataException("El archivo de metadatos es demasiado grande.");
        }

        using var lector = new StreamReader(flujo, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        string texto = lector.ReadToEnd();
        return Parsear(texto, lector.CurrentEncoding);
    }

    public static MetadatosDeDlc Parsear(string texto, Encoding codificacion)
    {
        var lector = new Lector(texto);
        lector.Exigir("dlcs");
        lector.Exigir("=");
        lector.Exigir("{");
        var entradas = new Dictionary<string, EntradaDeMetadatos>(StringComparer.Ordinal);

        Token clave = lector.Siguiente();
        while (clave.Valor != "}")
        {
            lector.ExigirEscalar(clave);
            lector.Exigir("=");
            lector.Exigir("{");
            uint? appId = null;
            Token campo = lector.Siguiente();
            while (campo.Valor != "}")
            {
                lector.ExigirEscalar(campo);
                lector.Exigir("=");
                Token valor = lector.Siguiente();
                if (campo.Valor == "steam_id")
                {
                    lector.ExigirEscalar(valor);
                    if (uint.TryParse(valor.Valor.Trim('"'), NumberStyles.None, CultureInfo.InvariantCulture, out uint id) && id > 0)
                    {
                        if (appId is not null)
                        {
                            throw new InvalidDataException($"La entrada {clave.Valor} contiene más de un App ID.");
                        }

                        appId = id;
                    }
                }
                else if (valor.Valor == "{")
                {
                    lector.SaltarBloque();
                }
                else
                {
                    lector.ExigirEscalar(valor);
                }

                campo = lector.Siguiente();
            }

            if (!entradas.TryAdd(clave.Valor, new EntradaDeMetadatos(clave.Valor, appId, clave.Inicio, texto[clave.Inicio..campo.Fin])))
            {
                throw new InvalidDataException($"La entrada {clave.Valor} aparece más de una vez.");
            }

            clave = lector.Siguiente();
        }

        lector.Exigir("");
        return new MetadatosDeDlc(texto, codificacion, entradas, clave.Inicio);
    }

    public bool Contiene(string clave, uint appId) =>
        entradas.TryGetValue(clave, out EntradaDeMetadatos? entrada) && entrada.AppId == appId;

    public bool MismoContenido(MetadatosDeDlc otro) =>
        Texto == otro.Texto && Codificacion.CodePage == otro.Codificacion.CodePage &&
        Codificacion.Preamble.SequenceEqual(otro.Codificacion.Preamble);

    public EntradaDeMetadatos Obtener(string clave, uint appId) =>
        Contiene(clave, appId) ? entradas[clave] :
            throw new InvalidDataException($"El metadata del ZIP debe contener {clave} con el App ID {appId}.");

    public string Incorporar(IEnumerable<EntradaDeMetadatos> aprobadas)
    {
        string salto = Texto.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var texto = new StringBuilder(Texto);
        var nuevas = new List<string>();
        var reemplazos = new List<(EntradaDeMetadatos Anterior, string Nuevo)>();
        foreach (EntradaDeMetadatos aprobada in aprobadas)
        {
            string bloque = aprobada.Bloque.ReplaceLineEndings(salto);
            if (entradas.TryGetValue(aprobada.Clave, out EntradaDeMetadatos? local))
            {
                if (local.AppId is uint id && id != aprobada.AppId)
                {
                    throw new InvalidDataException($"La clave {aprobada.Clave} ya pertenece a otro DLC.");
                }

                reemplazos.Add((local, bloque));
            }
            else
            {
                if (entradas.Values.Any(entrada => entrada.AppId == aprobada.AppId))
                {
                    throw new InvalidDataException($"El App ID {aprobada.AppId} ya figura con otra clave.");
                }

                nuevas.Add(bloque);
            }
        }

        if (nuevas.Count > 0)
        {
            texto.Insert(cierre, $"{salto}\t{string.Join(salto + "\t", nuevas)}{salto}");
        }

        foreach ((EntradaDeMetadatos anterior, string nuevo) in reemplazos.OrderByDescending(cambio => cambio.Anterior.Inicio))
        {
            texto.Remove(anterior.Inicio, anterior.Bloque.Length);
            texto.Insert(anterior.Inicio, nuevo);
        }

        return texto.ToString();
    }

    private readonly record struct Token(string Valor, int Inicio, int Fin);

    private sealed class Lector(string texto)
    {
        private int posicion;

        public Token Siguiente()
        {
            while (posicion < texto.Length)
            {
                if (char.IsWhiteSpace(texto[posicion]))
                {
                    posicion++;
                }
                else if (texto[posicion] == '#')
                {
                    while (posicion < texto.Length && texto[posicion] is not ('\r' or '\n'))
                    {
                        posicion++;
                    }
                }
                else
                {
                    break;
                }
            }

            int inicio = posicion;
            if (posicion == texto.Length)
            {
                return new Token("", inicio, inicio);
            }

            char primero = texto[posicion++];
            if (primero == '"')
            {
                while (posicion < texto.Length)
                {
                    char actual = texto[posicion++];
                    if (actual == '\\' && posicion < texto.Length)
                    {
                        posicion++;
                    }
                    else if (actual == '"')
                    {
                        return new Token(texto[inicio..posicion], inicio, posicion);
                    }
                }

                throw new InvalidDataException("Hay una cadena sin cerrar en los metadatos.");
            }

            if (primero is not ('{' or '}' or '='))
            {
                while (posicion < texto.Length && !char.IsWhiteSpace(texto[posicion]) &&
                    texto[posicion] is not ('{' or '}' or '=' or '#' or '"'))
                {
                    posicion++;
                }
            }

            return new Token(texto[inicio..posicion], inicio, posicion);
        }

        public void Exigir(string valor)
        {
            if (Siguiente().Valor != valor)
            {
                throw new InvalidDataException("La estructura del archivo de metadatos es inválida.");
            }
        }

        public void ExigirEscalar(Token token)
        {
            if (token.Valor is "" or "{" or "}" or "=")
            {
                throw new InvalidDataException("Falta un nombre o valor en los metadatos.");
            }
        }

        public void SaltarBloque()
        {
            int abiertas = 1;
            while (abiertas > 0)
            {
                switch (Siguiente().Valor)
                {
                    case "{": abiertas++; break;
                    case "}": abiertas--; break;
                    case "": throw new InvalidDataException("Hay un bloque sin cerrar en los metadatos.");
                }
            }
        }
    }
}
