namespace NexoFELRD.Web.Models;

public enum TipoIdentificacionFiscal
{
    Rnc = 1,
    Cedula = 2,
    Extranjero = 3
}

public enum TipoItemDgii
{
    Bien = 1,
    Servicio = 2
}

public enum TipoPagoDgii
{
    Contado = 1,
    Credito = 2,
    Gratuito = 3
}

public enum TipoIngresoDgii
{
    OperacionesNoFinancieras = 1,
    IngresosFinancieros = 2,
    IngresosExtraordinarios = 3,
    Arrendamientos = 4,
    VentaActivoDepreciable = 5,
    OtrosIngresos = 6
}

public enum FormaPagoDgii
{
    Efectivo = 1,
    ChequeTransferenciaDeposito = 2,
    TarjetaDebitoCredito = 3,
    VentaCredito = 4,
    BonoOCertificadoRegalo = 5,
    Permuta = 6,
    NotaCredito = 7,
    OtrasFormas = 8
}
