using NexoFELRD.Web.Models;

namespace NexoFELRD.Web.Services;

public static class ReglasReporteFiscal
{
    public static IQueryable<Factura> ConEfectoFiscal(
        this IQueryable<Factura> consulta)
    {
        return consulta.Where(factura =>
            factura.Estado == EstadoFactura.Confirmada ||
            factura.Estado == EstadoFactura.Generada ||
            (
                factura.Estado == EstadoFactura.Anulada &&
                factura.TipoComprobante != TipoComprobante.NotaDebito &&
                factura.TipoComprobante != TipoComprobante.NotaCredito &&
                factura.DocumentosRelacionados.Any(nota =>
                    nota.TipoComprobante == TipoComprobante.NotaCredito &&
                    nota.CodigoModificacion == CodigoModificacionDgii.AnulacionTotal &&
                    (
                        nota.Estado == EstadoFactura.Confirmada ||
                        nota.Estado == EstadoFactura.Generada
                    ))
            ));
    }

    public static IQueryable<MovimientoInventario> ReintegrosDeNotasCredito(
        this IQueryable<MovimientoInventario> consulta)
    {
        return consulta.Where(movimiento =>
            movimiento.Factura != null &&
            movimiento.Factura.TipoComprobante == TipoComprobante.NotaCredito &&
            (
                movimiento.Factura.Estado == EstadoFactura.Confirmada ||
                movimiento.Factura.Estado == EstadoFactura.Generada
            ) &&
            (
                movimiento.Factura.CodigoModificacion == CodigoModificacionDgii.AnulacionTotal ||
                movimiento.Factura.CodigoModificacion == CodigoModificacionDgii.CorreccionMontos
            ) &&
            movimiento.Tipo == TipoMovimiento.Entrada &&
            movimiento.Cantidad > 0 &&
            (
                movimiento.Motivo == MotivoMovimientoInventario.DevolucionCliente ||
                movimiento.Motivo == MotivoMovimientoInventario.AnulacionFactura
            ));
    }
}
