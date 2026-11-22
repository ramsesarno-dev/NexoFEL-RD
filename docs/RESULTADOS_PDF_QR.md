# Resultados del módulo PDF y QR interno

## Versión y alcance

Cambio preparado sobre `develop`, commit `637152b9d96b73dfef48d63f04c40da4c0031dd4`, el 22/11/2026. No se hizo commit, push, despliegue ni modificación del repositorio remoto desde este paquete.

La verificación utiliza .NET SDK 8.0.415, Linux x64 y SQL Server 2022 Developer `16.0.4325.4`. Las conexiones de integración apuntaron exclusivamente a una instancia temporal local y a bases aleatorias de pruebas. No se utilizó la base de desarrollo del usuario, SQLite ni EF InMemory.

## Resultado local de la versión entregada

| Comprobación | Resultado |
| --- | --- |
| Compilación Release de la solución | Correcta, 0 errores y 0 advertencias de compilación |
| Pruebas unitarias completas | 55 aprobadas, 0 fallidas, 0 omitidas |
| Pruebas de integración completas | 80 aprobadas, 0 fallidas, 0 omitidas |
| Total | 135 pruebas aprobadas |
| Casos nuevos respecto de la base | 30 unitarios y 15 de integración |
| Ejemplos PDF sintéticos | 8 documentos, 21 páginas renderizadas |
| Lectura independiente del QR sobre el renderizado | 8 QR leídos; dirección interna esperada en cada uno |
| Texto dentro de los márgenes de página | Correcto en las 21 páginas inspeccionadas por el verificador |
| Revisión visual | Factura E31/E32, crédito parcial, continuación de tabla, textos largos e importes grandes |
| Modelo y migraciones de base de datos | No modificados por este cambio |
| GitHub Actions del módulo PDF/QR | Completado posteriormente: 55 unitarias y 80 de integración aprobadas en el push `dfd46c6` y tras la integración en `e19f39c` |
| Instalación del PDF/QR en el Windows del usuario | Completada posteriormente: 55 unitarias y 80 de integración aprobadas, PDF de factura/nota y acceso autenticado revisados |

La cifra de 135 incluye las 90 pruebas anteriores; no se omitieron casos existentes para obtener un resultado verde. Los resultados `.trx` se incluyen en `evidencias/` del paquete, fuera de la carpeta de código para copiar.

Actualización del 22/11/2026: el módulo PDF/QR se publicó en `dfd46c62958efffaae060ab2d4b24b3d5500f2af` y se integró mediante el Pull Request #4 en `e19f39c3903ce7927cba42584b4614234d0a07e2`. Ejecuciones verificadas: https://github.com/ramsesarno-dev/NexoFEL-RD/actions/runs/45054717559 y https://github.com/ramsesarno-dev/NexoFEL-RD/actions/runs/45058103750. Esta evidencia pertenece al módulo anterior; no acredita una ejecución remota del nuevo cambio E45, documentado en `RESULTADOS_E45.md`.

## Pruebas unitarias nuevas

- Abrir el PDF real con PdfPig para verificar E31, E32, E33 y E34, número, comprador, líneas, importes e imagen de QR.
- Conservar un centavo histórico de diferencia y no leer nombre/precio/tasa actuales del catálogo.
- Nota parcial de una unidad con total RD$27.00, base RD$23.28 e ITBIS RD$3.72, y su referencia original.
- Corrección de texto por cero, crédito de más de 30 días, factura anulada y borrador explícito.
- Separación de monto no facturable y monto período.
- Paginación de 110 líneas con verificación de que el código y el final de su descripción permanecen en la misma página.
- Nombres/direcciones largos, importes grandes sin recortar, caracteres españoles y cultura del servidor distinta.
- Desactivación del QR y validación de su base, incluido un directorio virtual.
- Rechazo de URLs con esquema no permitido, credenciales, query, fragmento, barras invertidas o saltos de línea.
- QR sin datos fiscales ni credenciales y nombre de descarga sin inyección de rutas/cabeceras.

## Pruebas HTTP y SQL nuevas

- Administrador y Facturador descargan el archivo correcto, como adjunto y sin caché.
- Un visitante sin sesión debe ir al login; un usuario autenticado sin rol tiene el acceso denegado.
- IDs inexistentes o inválidos producen 404 sin escrituras.
- Cinco descargas simultáneas conservan el estado de facturas, detalles, pagos, productos, clientes, movimientos y rangos.
- Descargas de crédito parcial, nota de débito y factura anulada conservan contenido y estado.
- Cambiar catálogos no modifica los datos históricos del documento.
- Una cabecera Host manipulada no cambia el dominio configurado para el QR.
- Una configuración inválida devuelve un error 503 controlado sin escrituras de negocio.
- El detalle muestra el botón con la ruta del documento correcto.

La generación del QR se comprobó además con un lector independiente (ZXing-C++) sobre las páginas renderizadas, no solo buscando una URL en el texto PDF. Esta comprobación externa y la inspección visual no son tests de navegador ni se ejecutan en el workflow actual; los tests .NET sí se ejecutan allí cuando se suba el cambio.

## Mantenimiento pendiente, ajeno a este cambio

La auditoría NuGet conserva el hallazgo preexistente de severidad baja `GHSA-g4vj-cjjj-v7hg` en `NuGet.Packaging` y `NuGet.Protocol` `6.11.0`, transitivos de las herramientas del proyecto. No se actualizaron silenciosamente esas herramientas ni el framework en un cambio de documentos. Las nuevas dependencias PDF/QR/lector no fueron señaladas en esa auditoría.

Que el proyecto compile sin advertencias no significa que no existan avisos de mantenimiento o vulnerabilidades. El soporte de la plataforma, la revisión de dependencias, el despliegue, la custodia de claves y cualquier integración o certificación fiscal deben revisarse por separado antes de producción.

Los ejemplos del paquete son ficticios: sus datos de comprador, RNC y URL con ID 7 son solamente para inspección del diseño. No constituyen registros de negocio ni una validación del documento que pudiera tener ese ID en otra instalación.

## Referencias y webgrafía

- Guía de instalación y funcionamiento: `PDF_Y_QR.md`.
- QuestPDF, integración ASP.NET: https://www.questpdf.com/examples/aspnet-integration.html
- QuestPDF, licencia: https://www.questpdf.com/license/configuration.html
- QRCoder, código y licencia: https://github.com/Shane32/QRCoder
- PdfPig, lector PDF usado en tests: https://github.com/UglyToad/PdfPig
- ZXing-C++, lector independiente para la revisión de QR: https://github.com/zxing-cpp/zxing-cpp
- Aviso preexistente de NuGet: https://github.com/advisories/GHSA-g4vj-cjjj-v7hg
