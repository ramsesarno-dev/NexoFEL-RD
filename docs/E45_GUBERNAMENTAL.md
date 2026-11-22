# E45 - Comprobante gubernamental

## Alcance de esta entrega

Extiende la versión base `e19f39c3903ce7927cba42584b4614234d0a07e2`. E45 se emite desde **Facturas > Nueva factura**. E31 y E32 conservan su flujo; las notas E33/E34 se generan desde el detalle del original. E41, E43, E44, E46 y E47 siguen pendientes: tener esos códigos en el enum no los habilita.

Es un flujo interno del proyecto académico, no una implementación completa del formato XML oficial ni una certificación de emisor electrónico. No firma, transmite, consulta ni obtiene aceptación ante DGII. No se hace una consulta del padrón de contribuyentes ni del carácter gubernamental del comprador.

## Reglas implementadas

- Solo pueden emitir Administrador y Facturador; se conserva la antifalsificación y el token de operación.
- El comprador debe existir, estar activo y tener nombre o razón social e identificación fiscal válida según la validación estructural local. Se admite el formato con espacios/guiones, normalizando a dígitos. No se admiten letras ni se sustituye el RNC por un identificador extranjero.
- El operador debe confirmar que el destinatario es una entidad gubernamental que no realiza actividad comercial. Es una declaración explícita, no una verificación automática ante DGII. Se exige también cuando se envía el POST directamente.
- Se conservan las condiciones de pago existentes. Para crédito, se exige fecha límite igual o posterior a la emisión. El tipo E45 no aplica una exención automática ni una retención fija: base e ITBIS se calculan en el servidor según el indicador y tasa del producto y el tratamiento del precio.
- El e-NCF se toma únicamente de un rango E45 activo, autorizado, vigente para la fecha de emisión y con secuencias disponibles. No se toma un rango E31/E32 como alternativa. El emisor debe disponer de autorización real cuando corresponda; un rango de demostración no acredita autorización de DGII.
- Los bienes descuentan existencias y generan salida trazable; los servicios no. Factura, detalle, secuencia y movimiento se guardan en la misma transacción.
- Se conservan los datos históricos del comprador y de las líneas. El PDF usa esa información, no el catálogo actual. El archivo se identifica como `gubernamental-E45....pdf` y mantiene el QR de consulta interna con sesión.
- El detalle permite nota de débito y nota de crédito. El débito agrega cargos sin inventar salidas de bienes; el crédito parcial o total conserva el tratamiento previo de cantidades e inventario.
- Las devoluciones parciales reparten el total histórico sin sumar un centavo por redondear base e ITBIS independientemente; la última devolución conserva los saldos pendientes. La corrección también cubre E31/E32, sin reescribir notas existentes.
- Los reportes incluyen E45 como venta y sus notas con el signo correspondiente. El dashboard incorpora E45 a las ventas y al contador diario, según la fecha comercial dominicana.
- Reenviar el mismo formulario, incluso simultáneamente, encuentra el documento existente. Cambiar datos, incluida la declaración gubernamental, con una clave ya usada devuelve conflicto. E31/E32 mantienen su huella anterior.

## Instalación sin modificar la base existente

1. Detener la aplicación y guardar los documentos abiertos en Visual Studio.
2. Confirmar que se parte de `e19f39c` y que no hay modificaciones sin guardar en Git. Si hay cambios posteriores, revisar el parche antes de copiar archivos; no sobrescribirlos.
3. Crear una rama local de trabajo, por ejemplo `feature/e45-gubernamental`.
4. Copiar solo el contenido de la carpeta `archivos` del ZIP a la raíz del repositorio, conservando rutas. Como alternativa, aplicar `cambios.patch` después de comprobarlo con `git apply --check`. No utilizar ambas opciones.
5. Si Visual Studio detecta cambios externos, pulsar **Reload**.
6. Restaurar, compilar y ejecutar las pruebas. Las instrucciones completas están en `tests/README.md` y los resultados de esta entrega en `RESULTADOS_E45.md`.

No se incluyen `appsettings`, User Secrets, credenciales, bases de datos ni binarios. No hay cambios en entidades persistidas, migraciones, solución, proyectos o workflow. No ejecutar `Add-Migration`, crear otra migración inicial, eliminar la base ni recrearla por este cambio.

## Comprobación manual con datos de demostración

Usar una base de demostración o una copia de pruebas, nunca crear movimientos ficticios en datos reales. No hace falta volver a registrar los comprobantes anteriores.

1. Crear un comprador de prueba con nombre `Institución pública simulada` e identificación `400000005`. Es un dato sintético con dígito verificador calculado, sin afirmación de que exista o esté inscrito en DGII. No usarlo para facturación real.
2. Como Administrador, registrar en **Secuencias e-NCF** un rango E45 de prueba, activo y con fechas válidas. Este registro local no solicita ni obtiene autorización fiscal.
3. En **Nueva factura**, seleccionar el comprador, E45 y uno o varios productos. Marcar la declaración gubernamental y confirmar.
4. Comprobar que se asignó E45, no E31/E32, y que el inventario solo disminuye para bienes.
5. Abrir el detalle y descargar el PDF. Debe mostrar el comprador, el e-NCF E45 y los mismos importes. El QR seguirá siendo interno; `localhost` no permite consultar el equipo desde otro dispositivo.
6. Revisar dashboard y reportes. Si se usa otra fecha de emisión, ajustar el período del reporte; el dashboard muestra el día comercial dominicano.
7. Cuando sea necesario demostrar ajustes, crear una nota desde el detalle de E45. No utilizar el selector de Nueva factura para crear E33/E34 independientes.

La aceptación en Windows y la nueva ejecución de GitHub Actions permanecen pendientes hasta instalar y publicar esta entrega. No se han hecho commits, push, cambios de protección de ramas ni despliegues desde este paquete.

## Fuentes y webgrafía

- DGII, Tipos y estructura de e-CF: https://dgii.gov.do/cicloContribuyente/facturacion/comprobantesFiscalesElectronicosE-CF/Paginas/TipoyEstructurae-CF.aspx
- DGII, Formato Comprobante Fiscal Electrónico, versión 1.0 (octubre de 2025), apartados Identificación del documento, Comprador y Detalle: https://dgii.gov.do/cicloContribuyente/facturacion/comprobantesFiscalesElectronicosE-CF/Documentacin%20sobre%20eCF/Formatos%20XML/Formato%20Comprobante%20Fiscal%20Electr%C3%B3nico%20(e-CF)%20V1.0.pdf
- DGII, catálogo de documentación y esquemas por tipo: https://dgii.gov.do/cicloContribuyente/facturacion/comprobantesFiscalesElectronicosE-CF/Paginas/documentacionSobreE-CF.aspx

Las fuentes orientan el subconjunto implementado. Esta entrega no genera XML ni ejecuta una validación XSD del documento completo.
