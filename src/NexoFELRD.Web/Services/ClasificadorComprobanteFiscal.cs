using NexoFELRD.Web.Models;

namespace NexoFELRD.Web.Services;

public enum CategoriaReporteFiscal
{
    Venta = 1,
    Compra = 2,
    Gasto = 3,
    PagoExterior = 4,
    Otro = 99
}

public static class ClasificadorComprobanteFiscal
{
    public static CategoriaReporteFiscal ObtenerCategoria(
        TipoComprobante tipoComprobante,
        TipoComprobante? tipoComprobanteAfectado = null)
    {
        /*
         * En una nota E33 o E34, la categoría depende
         * del comprobante original que está modificando.
         */
        if ((tipoComprobante == TipoComprobante.NotaDebito ||
             tipoComprobante == TipoComprobante.NotaCredito) &&
            tipoComprobanteAfectado.HasValue)
        {
            return ObtenerCategoriaBase(
                tipoComprobanteAfectado.Value);
        }

        return ObtenerCategoriaBase(tipoComprobante);
    }

    public static int ObtenerSigno(
        TipoComprobante tipoComprobante)
    {
        return tipoComprobante ==
            TipoComprobante.NotaCredito
                ? -1
                : 1;
    }

    public static bool EsAjuste(
        TipoComprobante tipoComprobante)
    {
        return tipoComprobante ==
                   TipoComprobante.NotaDebito ||
               tipoComprobante ==
                   TipoComprobante.NotaCredito;
    }

    public static bool EsDocumentoBaseVenta(
        TipoComprobante tipoComprobante)
    {
        return tipoComprobante switch
        {
            TipoComprobante.CreditoFiscal => true,
            TipoComprobante.Consumo => true,
            TipoComprobante.RegimenesEspeciales => true,
            TipoComprobante.Gubernamental => true,
            TipoComprobante.Exportaciones => true,
            _ => false
        };
    }

    public static string ObtenerCodigo(
        TipoComprobante tipoComprobante)
    {
        return tipoComprobante switch
        {
            TipoComprobante.CreditoFiscal => "E31",
            TipoComprobante.Consumo => "E32",
            TipoComprobante.NotaDebito => "E33",
            TipoComprobante.NotaCredito => "E34",
            TipoComprobante.Compras => "E41",
            TipoComprobante.GastosMenores => "E43",
            TipoComprobante.RegimenesEspeciales => "E44",
            TipoComprobante.Gubernamental => "E45",
            TipoComprobante.Exportaciones => "E46",
            TipoComprobante.PagosExterior => "E47",
            _ => "e-CF"
        };
    }

    public static string ObtenerNombre(
        TipoComprobante tipoComprobante)
    {
        return tipoComprobante switch
        {
            TipoComprobante.CreditoFiscal =>
                "Crédito fiscal",

            TipoComprobante.Consumo =>
                "Consumo",

            TipoComprobante.NotaDebito =>
                "Nota de débito",

            TipoComprobante.NotaCredito =>
                "Nota de crédito",

            TipoComprobante.Compras =>
                "Compras",

            TipoComprobante.GastosMenores =>
                "Gastos menores",

            TipoComprobante.RegimenesEspeciales =>
                "Regímenes especiales",

            TipoComprobante.Gubernamental =>
                "Gubernamental",

            TipoComprobante.Exportaciones =>
                "Exportaciones",

            TipoComprobante.PagosExterior =>
                "Pagos al exterior",

            _ => "Comprobante"
        };
    }

    public static string ObtenerNombreCategoria(
        CategoriaReporteFiscal categoria)
    {
        return categoria switch
        {
            CategoriaReporteFiscal.Venta =>
                "Ventas",

            CategoriaReporteFiscal.Compra =>
                "Compras",

            CategoriaReporteFiscal.Gasto =>
                "Gastos",

            CategoriaReporteFiscal.PagoExterior =>
                "Pagos al exterior",

            _ => "Otros"
        };
    }

    private static CategoriaReporteFiscal
        ObtenerCategoriaBase(
            TipoComprobante tipoComprobante)
    {
        return tipoComprobante switch
        {
            TipoComprobante.CreditoFiscal =>
                CategoriaReporteFiscal.Venta,

            TipoComprobante.Consumo =>
                CategoriaReporteFiscal.Venta,

            TipoComprobante.NotaDebito =>
                CategoriaReporteFiscal.Venta,

            TipoComprobante.NotaCredito =>
                CategoriaReporteFiscal.Venta,

            TipoComprobante.RegimenesEspeciales =>
                CategoriaReporteFiscal.Venta,

            TipoComprobante.Gubernamental =>
                CategoriaReporteFiscal.Venta,

            TipoComprobante.Exportaciones =>
                CategoriaReporteFiscal.Venta,

            TipoComprobante.Compras =>
                CategoriaReporteFiscal.Compra,

            TipoComprobante.GastosMenores =>
                CategoriaReporteFiscal.Gasto,

            TipoComprobante.PagosExterior =>
                CategoriaReporteFiscal.PagoExterior,

            _ => CategoriaReporteFiscal.Otro
        };
    }
}
