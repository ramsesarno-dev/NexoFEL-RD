using NexoFELRD.Web.Models;

namespace NexoFELRD.Web.Services;

// El catálogo DGII incluye tipos cuyos flujos todavía no están implementados.
public static class ReglasEmisionVenta
{
    public static bool EsTipoHabilitado(TipoComprobante tipo) => tipo is
        TipoComprobante.CreditoFiscal or
        TipoComprobante.Consumo or
        TipoComprobante.Gubernamental;

    public static bool RequiereCompradorIdentificado(TipoComprobante tipo) => tipo is
        TipoComprobante.CreditoFiscal or
        TipoComprobante.Gubernamental;
}
