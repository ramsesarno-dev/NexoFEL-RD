using NexoFELRD.Web.Models;

namespace NexoFELRD.Web.Services;

public static class DistribucionCredito
{
    // Reparte importes históricos, nunca precios ni tasas del catálogo actual.
    // Redondear base e ITBIS independientemente puede inventar un centavo.
    public static (decimal Subtotal, decimal Itbis) Repartir(
        FacturaDetalle original, decimal cantidad, decimal cantidadDisponible,
        decimal subtotalDisponible, decimal itbisDisponible)
    {
        ArgumentNullException.ThrowIfNull(original);
        if (original.Cantidad <= 0 || cantidad <= 0 || cantidad > cantidadDisponible ||
            cantidadDisponible > original.Cantidad || subtotalDisponible < 0 || itbisDisponible < 0)
            throw new ArgumentOutOfRangeException(nameof(cantidad), "El saldo o la cantidad de crédito no es válido.");

        if (cantidad == cantidadDisponible)
            return (Redondear(subtotalDisponible), Redondear(itbisDisponible));

        var totalDisponible = Redondear(subtotalDisponible + itbisDisponible);
        var total = Math.Min(totalDisponible,
            Redondear((original.Subtotal + original.Itbis) * cantidad / original.Cantidad));
        var subtotalProporcional = Redondear(original.Subtotal * cantidad / original.Cantidad);
        // Respeta ambos saldos incluso con importes mínimos o notas históricas
        // que distribuyeron el redondeo de otra forma.
        var subtotal = Math.Clamp(subtotalProporcional,
            Math.Max(0, total - itbisDisponible), Math.Min(total, subtotalDisponible));
        return (subtotal, Redondear(total - subtotal));
    }

    private static decimal Redondear(decimal valor) =>
        Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
