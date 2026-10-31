namespace NexoFELRD.Web.Models;

public static class CatalogosDgii
{
    public static IReadOnlyDictionary<byte, string>
        IndicadoresFacturacion
    { get; } =
        new Dictionary<byte, string>
        {
            [0] = "No facturable",
            [1] = "ITBIS 18%",
            [2] = "ITBIS 16%",
            [3] = "ITBIS 0%",
            [4] = "Exento"
        };

    public static IReadOnlyDictionary<byte, string>
        UnidadesMedida
    { get; } =
        new Dictionary<byte, string>
        {
            [1] = "Barril",
            [2] = "Bolsa",
            [3] = "Bote",
            [4] = "Bultos",
            [5] = "Botella",
            [6] = "Caja/Cajón",
            [7] = "Cajetilla",
            [8] = "Centímetro",
            [9] = "Cilindro",
            [10] = "Conjunto",
            [11] = "Contenedor",
            [12] = "Día",
            [13] = "Docena",
            [14] = "Fardo",
            [15] = "Galones",
            [16] = "Grado",
            [17] = "Gramo",
            [18] = "Granel",
            [19] = "Hora",
            [20] = "Huacal",
            [21] = "Kilogramo",
            [22] = "Kilovatio hora",
            [23] = "Libra",
            [24] = "Litro",
            [25] = "Lote",
            [26] = "Metro",
            [27] = "Metro cuadrado",
            [28] = "Metro cúbico",
            [29] = "Millones de unidades térmicas",
            [30] = "Minuto",
            [31] = "Paquete",
            [32] = "Par",
            [33] = "Pie",
            [34] = "Pieza",
            [35] = "Rollo",
            [36] = "Sobre",
            [37] = "Segundo",
            [38] = "Tanque",
            [39] = "Tonelada",
            [40] = "Tubo",
            [41] = "Yarda",
            [42] = "Yarda cuadrada",
            [43] = "Unidad",
            [44] = "Elemento",
            [45] = "Millar",
            [46] = "Saco",
            [47] = "Lata",
            [48] = "Display",
            [49] = "Bidón",
            [50] = "Ración",
            [51] = "Quintal",
            [52] = "Toneladas de registro bruto",
            [53] = "Pie cuadrado",
            [54] = "Pasajero",
            [55] = "Pulgadas",
            [56] = "Parqueo de barcos en muelle",
            [57] = "Bandeja",
            [58] = "Hectárea",
            [59] = "Mililitro",
            [60] = "Miligramo",
            [61] = "Onzas",
            [62] = "Onzas Troy"
        };

    public static bool EsIndicadorFacturacionValido(
        byte codigo)
    {
        return IndicadoresFacturacion.ContainsKey(codigo);
    }

    public static bool EsUnidadMedidaValida(
        byte? codigo)
    {
        return codigo is null ||
               UnidadesMedida.ContainsKey(codigo.Value);
    }

    public static decimal ObtenerTasaItbis(
        byte indicadorFacturacion)
    {
        return indicadorFacturacion switch
        {
            0 => 0m,
            1 => 18m,
            2 => 16m,
            3 => 0m,
            4 => 0m,
            _ => throw new ArgumentOutOfRangeException(
                nameof(indicadorFacturacion))
        };
    }
}
