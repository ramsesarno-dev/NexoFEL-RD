# Resultados de E45 Gubernamental

## Versión y entorno

Entrega preparada el 22/11/2026 sobre `e19f39c3903ce7927cba42584b4614234d0a07e2`, en una copia separada del repositorio y rama local `feature/e45-gubernamental`. No se hizo commit, push, Pull Request, despliegue ni cambio de configuración en GitHub desde esta entrega.

Verificado en Linux x64, .NET SDK 8.0.415 y SQL Server 2022 Developer. El servidor SQL fue exclusivo de esta revisión, accesible por `127.0.0.1:14337`, con contraseña aleatoria conservada solo en memoria. Los casos utilizaron bases `NexoFELRD_Tests_` con GUID y la protección de limpieza existente. El servidor temporal quedó detenido al terminar. No se utilizó ni modificó la base de desarrollo del usuario.

## Resultado final completo

| Comprobación | Resultado |
| --- | --- |
| Compilación Release | Correcta, 0 errores y 0 advertencias de compilación |
| Pruebas unitarias | 83 aprobadas, 0 fallidas, 0 omitidas |
| Pruebas de integración con SQL Server | 129 aprobadas, 0 fallidas, 0 omitidas |
| Total | 212 pruebas aprobadas |
| Casos adicionales respecto de la base | 28 unitarios y 49 de integración |
| Casos anteriores conservados | Las 135 comprobaciones anteriores siguen incluidas |
| PDF E45 sintético | Una página, encabezado, comprador e importes comprobados y revisión visual realizada |
| QR renderizado | Leído con ZXing-C++, URL interna esperada y texto dentro de márgenes |
| JavaScript del formulario | Sintaxis y transiciones E32/E45/E31 comprobadas con controles simulados |
| Modelo persistido y migraciones | Sin cambios |
| Windows del usuario con E45 | Pendiente de instalar y comprobar esta entrega |
| GitHub Actions del nuevo código E45 | Pendiente de publicar la rama y abrir el Pull Request |

Los `.trx` finales, el resumen del lector PDF/QR y la comprobación de JavaScript se incluyen en `evidencias/` del paquete. El PDF y la vista previa son sintéticos, no registros del negocio ni documentos aceptados por DGII.

## Cobertura nueva

- Emisión E45 por ambos roles autorizados, datos históricos del comprador, existencia y consumo exclusivo del rango E45.
- Rechazo de comprador ausente, inexistente, inactivo, sin nombre, sin identificación, identificación inválida o con letras.
- Declaración gubernamental obligatoria aun en POST directo, normalización de identificación formateada y conservación de la selección después de un error.
- Tasas de catálogo 18 %, 16 %, 0 % y exento, con impuestos incluidos/no incluidos; no se fuerza una exención por el solo hecho de seleccionar E45.
- Servicios sin salida de bienes y conservación de existencias, fechas de crédito y rechazo de importes/precios/identificación manipulados en el navegador.
- Rangos inexistentes, inactivos, agotados, vencidos o de autorización futura; agotamiento real de la última secuencia disponible.
- Bloqueo de tipos no implementados enviados mediante POST, antifalsificación y formulario con E31/E32/E45.
- Reenvíos secuenciales y simultáneos sin duplicación, y conflicto al alterar la declaración gubernamental de un formulario ya procesado.
- Compatibilidad exacta de las huellas anteriores E31/E32.
- Crédito parcial, anulación total, débito, PDF histórico que no se altera al modificar catálogos, filtro de reportes y dashboard por fecha comercial dominicana.
- Prorrateo de importes mínimos, saldos finales y conservación de centavos históricos en notas de crédito.
- Dos devoluciones parciales sucesivas sobre E31, E32 y E45: los importes acumulados de base, impuesto y total coinciden con el documento original.

## Hallazgos corregidos durante la revisión

1. El dashboard no incluía E45 en sus ventas y contador diarios. Se incorporó y se agregó una comprobación con la fecha comercial dominicana.
2. En una venta de dos unidades de RD$27.00, con base histórica RD$46.55 e ITBIS RD$7.45, redondear base e impuesto independientemente producía un crédito de RD$27.01 por una unidad. Ahora se reparte el total histórico y se deriva el impuesto restante sin superar saldos; la última devolución absorbe el residuo de redondeo. No se recalculan importes desde el catálogo actual ni se reescriben documentos ya guardados. Se mantiene el tratamiento existente de notas mayores de 30 días.
3. Al ampliar la batería, dos casos no podían iniciar por el límite de 128 instancias inotify del entorno Linux. El proceso de integración utiliza ahora `DOTNET_USE_POLLING_FILE_WATCHER=1` desde su infraestructura de pruebas. No se cambiaron permisos, límites del sistema, configuración de la aplicación normal ni expectativas de los casos. La ejecución final de los 129 casos terminó completa.
4. El título gubernamental era más largo que los títulos anteriores del PDF. Se ajustó su tamaño para que quepa sin dividir el encabezado; se conservan los demás formatos.

## Límites de esta comprobación

- No equivale a una certificación de cumplimiento tributario, auditoría de seguridad completa o prueba de carga.
- La declaración del operador no verifica en línea que el comprador sea una entidad gubernamental. La identificación sintética no acredita inscripción en el RNC.
- No se implementa una exención general ni una retención automática para E45. Los escenarios solo verifican los cálculos y datos admitidos por el prototipo.
- No se genera XML, firma, comunicación, acuse ni aceptación ante DGII, ni se validó un documento XML completo con XSD.
- Las pruebas HTTP se ejecutan con la aplicación real hospedada por TestServer. La comprobación JavaScript con controles simulados no es una prueba E2E en un navegador real.
- La versión .NET y las dependencias existentes se conservaron. Antes de producción deben revisarse su soporte y los hallazgos de mantenimiento ya descritos en `RESULTADOS_PDF_QR.md`; compilar sin advertencias no equivale a no tener vulnerabilidades.

## Referencias y webgrafía

- Guía del módulo y fuentes DGII: `E45_GUBERNAMENTAL.md`.
- Microsoft, File Providers en ASP.NET Core, alternativa de sondeo documentada: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/file-providers?view=aspnetcore-8.0
- DGII, documentación oficial de e-CF: https://dgii.gov.do/cicloContribuyente/facturacion/comprobantesFiscalesElectronicosE-CF/Paginas/documentacionSobreE-CF.aspx
- ZXing-C++, lector independiente utilizado: https://github.com/zxing-cpp/zxing-cpp
