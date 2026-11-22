# PDF de comprobantes y QR interno

## Qué se agregó

En **Facturas > Ver detalle**, el botón **Descargar PDF** devuelve un archivo PDF real. Funciona con facturas y notas de crédito/débito ya registradas. El botón anterior **Imprimir** sigue disponible.

El documento incluye:

- Datos configurables del emisor, comprador histórico, número interno, tipo, e-NCF, estado y fecha.
- Condición de pago, tratamiento del precio y, cuando están guardados, vencimiento y términos.
- Productos/servicios históricos, cantidades, unidad, precio de cuatro decimales, base, ITBIS y total de línea.
- Descuentos y recargos registrados, clasificación tributaria y formas de pago cuando existen.
- Subtotal facturable, ITBIS, monto no facturable, monto total y monto período cuando corresponde.
- Comprobante original, motivo y código de modificación de las notas.
- QR y enlace de consulta interna, paginación y encabezados de tabla repetidos.

No recalcula impuestos ni corrige diferencias históricas. Un comprobante guardado por RD$162.01 se descarga por RD$162.01, aunque el producto actual tenga otro precio. Los datos del comprador y los ítems provienen de la factura, no del catálogo actual.

## Instalar este cambio sobre develop

Base utilizada: `637152b9d96b73dfef48d63f04c40da4c0031dd4` (acciones actualizadas a Node.js 24).

1. Detener la aplicación y guardar los cambios locales propios. Si hay cambios posteriores en un archivo que el paquete reemplaza, comparar antes de sobrescribir.
2. Copiar **el contenido** de `archivos` en la raíz que contiene `NexoFELRD.sln`. No copiar la carpeta `archivos` como una subcarpeta del proyecto.
3. Aceptar el reemplazo solamente de los archivos enumerados en `MANIFIESTO_SHA256.txt` del paquete y que correspondan a la versión base. No copiar `.vs`, `.git`, `bin`, `obj` ni `TestResults` a las carpetas de código.
4. En PowerShell, desde la raíz del repositorio, ejecutar:

```powershell
dotnet restore NexoFELRD.sln --disable-parallel --property:BuildInParallel=false
dotnet build NexoFELRD.sln --configuration Release --no-restore --maxcpucount:1
dotnet test tests/NexoFELRD.UnitTests/NexoFELRD.UnitTests.csproj --configuration Release --no-build
```

5. Iniciar la aplicación y abrir un comprobante existente. Debe aparecer **Descargar PDF**.

**Este cambio no agrega migraciones ni modifica el modelo de base de datos. No ejecutar Add-Migration, Update-Database, borrar ni recrear la base por este módulo.** La migración anterior de idempotencia debe estar ya aplicada, como en la versión base.

No se cambia el workflow: los dos proyectos de pruebas ya son ejecutados completos por GitHub Actions. Los archivos nuevos se incluyen automáticamente gracias al SDK de .NET.

## Configurar el emisor y la dirección del QR

De forma predeterminada se muestra `NexoFEL RD (demostración)` y se advierte que el RNC del emisor no está configurado. No se inventa un RNC de negocio.

En desarrollo se pueden utilizar User Secrets. Sustituir el contenido entre corchetes por datos propios; no ejecutar el ejemplo con esos marcadores literales:

```powershell
dotnet user-secrets set 'DocumentosPdf:NombreEmisor' '[Nombre de la empresa]' --project src/NexoFELRD.Web
dotnet user-secrets set 'DocumentosPdf:RncEmisor' '[RNC de la empresa]' --project src/NexoFELRD.Web
dotnet user-secrets set 'DocumentosPdf:DireccionEmisor' '[Dirección de la empresa]' --project src/NexoFELRD.Web
dotnet user-secrets set 'DocumentosPdf:TelefonoEmisor' '[Teléfono de la empresa]' --project src/NexoFELRD.Web
dotnet user-secrets set 'DocumentosPdf:CorreoEmisor' '[Correo de la empresa]' --project src/NexoFELRD.Web
dotnet user-secrets set 'DocumentosPdf:UrlBaseConsulta' 'https://localhost:7184/' --project src/NexoFELRD.Web
```

Reiniciar la aplicación después de cambiar la configuración.

| Opción | Uso |
| --- | --- |
| `NombreEmisor` | Obligatorio; máximo 150 caracteres. |
| `RncEmisor` | Opcional; máximo 20 caracteres. No se valida contra un servicio de DGII. |
| `DireccionEmisor` | Opcional; máximo 250 caracteres. |
| `TelefonoEmisor` | Opcional; máximo 40 caracteres. |
| `CorreoEmisor` | Opcional; máximo 120 caracteres. |
| `UrlBaseConsulta` | URL absoluta HTTP/HTTPS, hasta 700 caracteres, sin usuario, clave, query ni fragmento. Vacía desactiva el QR. |

En un despliegue, configurar esas mismas opciones en la configuración del servidor, por ejemplo `DocumentosPdf__NombreEmisor` y `DocumentosPdf__UrlBaseConsulta`. Si la aplicación vive en `https://mi-servidor.example/nexofel/`, usar esa base completa: se conserva `/nexofel/`.

La dirección predeterminada del QR es `https://localhost:7184/`, correspondiente al entorno local conocido. **Un teléfono que escanee localhost intentará abrir el propio teléfono, no la computadora.** Para consultarlo desde otro equipo se necesita una dirección realmente accesible, con red y certificado correctamente configurados. Este módulo no abre puertos, configura certificados ni publica la aplicación automáticamente.

El QR contiene solamente la URL del detalle y el ID interno. No contiene RNC, importes, claves ni tokens de sesión. Quien lo abra tendrá que iniciar sesión con un rol permitido. La URL nunca se construye con `Request.Host`; una cabecera Host manipulada no puede sustituir el dominio del QR.

Para desactivar el QR, definir `DocumentosPdf:UrlBaseConsulta` como cadena vacía en la configuración de la aplicación. El PDF sigue descargándose y lo indica expresamente. Una URL inválida no genera un enlace falso: devuelve un error 503 controlado sin modificar la operación.

Los datos del emisor proceden de la configuración vigente al descargar, **no de una instantánea histórica por factura**. Si se necesitan emisores históricos, múltiples empresas o reproducciones archivadas inmutables, se requiere otro diseño. No mezclar comprobantes de empresas diferentes cambiando simplemente la configuración.

## Seguridad y límites fiscales

- `GET /Facturas/DescargarPdf/{id}` conserva la autorización del controlador: Administrador o Facturador, con el mismo alcance de consulta que el detalle actual.
- La respuesta es `application/pdf`, como adjunto, con nombre saneado, `Cache-Control: no-store, no-cache` y `X-Content-Type-Options: nosniff`.
- Una factura inexistente devuelve 404. No se crea una factura para descargarla.
- Solo se ejecutan consultas `AsNoTracking`; no se llama a `SaveChanges`, al proceso de emisión ni a un servicio externo.
- No se guardan PDFs en `wwwroot`, no se publican carpetas de descargas y no se llama a una API de QR en Internet.
- No se envían documentos a DGII, no se genera XML fiscal ni firma digital, y no se demuestra certificación o aceptación de DGII.
- Las notas conservan sus importes positivos registrados, identificando su efecto de crédito/débito. No se convierten artificialmente a una factura negativa.
- Una factura anulada sigue disponible para consulta y el PDF muestra su estado. Un borrador se identifica como sin validez fiscal.
- La descarga no prueba un reembolso, pago o reintegro físico; se muestran únicamente los datos de la operación y, para inventario, se remite al historial.

## Dependencias y licencia

- Aplicación: QuestPDF `2026.9.2` para composición PDF y QRCoder `1.9.0` para PNG local del QR.
- Pruebas únicamente: PdfPig `0.1.16`, lector independiente para abrir los PDFs y verificar contenido y páginas.
- No es necesario instalar Chrome, wkhtmltopdf ni componentes de impresión de Windows. Se utilizan las fuentes Lato distribuidas con QuestPDF, no fuentes del sistema.
- QuestPDF se configura con licencia Community para este proyecto académico. Su modelo no es simplemente MIT; revisar la elegibilidad y los términos oficiales antes de reutilizarlo comercialmente. QRCoder es MIT; PdfPig es Apache-2.0.

## Pruebas en Windows

Unitarias:

```powershell
dotnet test tests/NexoFELRD.UnitTests/NexoFELRD.UnitTests.csproj --configuration Release
```

Integración, únicamente contra una instancia local de pruebas:

```powershell
$env:NEXOFEL_TEST_SQL_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True'
dotnet test tests/NexoFELRD.IntegrationTests/NexoFELRD.IntegrationTests.csproj --configuration Release --logger 'trx;LogFileName=integracion-pdf.trx' --results-directory TestResults/pdf-integracion
```

Cada caso usa una base aleatoria propia y la elimina al terminar. No usar la conexión de desarrollo o producción.

Para generar ejemplos **sintéticos**, opcionalmente:

```powershell
$env:NEXOFEL_PDF_EJEMPLOS_DIR = Join-Path $env:TEMP ('NexoFEL-PDF-' + [guid]::NewGuid().ToString('N'))
dotnet test tests/NexoFELRD.UnitTests/NexoFELRD.UnitTests.csproj --configuration Release
explorer $env:NEXOFEL_PDF_EJEMPLOS_DIR
Remove-Item Env:NEXOFEL_PDF_EJEMPLOS_DIR
```

Los tests de PDF crean ejemplos solo cuando está definida esa variable. No se crean documentos de negocio ni se modifica la base.

Comprobación manual breve:

1. Abrir una factura anterior y descargar: comparar número, e-NCF y totales con el detalle.
2. Descargar una nota parcial: debe mostrar la unidad acreditada y la referencia original, sin anular ni volver a acreditar.
3. Repetir la descarga y comprobar que no aparecen movimientos nuevos.
4. Abrir el enlace del QR sin sesión: debe pedir login; con sesión permitida debe abrir el detalle.
5. Si se configura un servidor accesible, escanear desde otro dispositivo. No esperar que un QR de localhost funcione desde un teléfono.

## Referencias técnicas y webgrafía

- QuestPDF. Integración con ASP.NET: https://www.questpdf.com/examples/aspnet-integration.html
- QuestPDF. Licencia y configuración: https://www.questpdf.com/license/configuration.html
- QuestPDF. Tabla y paginación: https://www.questpdf.com/api-reference/table/basics.html
- QuestPDF. Versión utilizada: https://www.nuget.org/packages/QuestPDF/2026.9.2
- QRCoder. Repositorio oficial y licencia MIT: https://github.com/Shane32/QRCoder
- QRCoder. Versión utilizada: https://www.nuget.org/packages/QRCoder/1.9.0
- PdfPig. Repositorio oficial: https://github.com/UglyToad/PdfPig
- PdfPig. Versión utilizada: https://www.nuget.org/packages/PdfPig/0.1.16
