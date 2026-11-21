# Resultado de la automatización y corrección de idempotencia

Fecha de revisión: 21 de noviembre de 2026.

Código base: `develop`, commit `42d651bc781a0202fa30ccf281da8446931287cd`, más el paquete de automatización y la corrección de idempotencia.

## Resultado observado

| Comprobación | Resultado |
| --- | --- |
| Compilación Release, con un proceso de MSBuild | Correcta: cero errores y cero advertencias |
| Pruebas unitarias | 25 aprobadas de 25 |
| Pruebas de integración con SQL Server | 65 aprobadas de 65 |
| Pruebas omitidas | 0 |
| Total de casos | 90 aprobados, 0 fallidos |
| Ejecución previa, antes de ampliar con compatibilidad de migración y dos hosts | 63 de 63 pruebas de integración aprobadas |
| Repetición de la batería final completa de integración | 65 de 65 aprobadas, sin omisiones |
| Modelo EF Core respecto de la última migración | Sin cambios pendientes |
| Sintaxis del YAML y de los scripts Bash del workflow | Verificada localmente |
| Ejecución del workflow dentro de GitHub | Pendiente: no se hizo push ni se ejecutaron acciones en la cuenta del usuario |

La ejecución utilizó SQL Server 2022 Developer `16.0.4325.4`, .NET SDK `8.0.415` y bases temporales independientes. La base de desarrollo del usuario no se utilizó ni se modificó. No se sustituyó SQL Server por EF InMemory o SQLite.

## Hallazgo original 1, corregido: reenvío secuencial de una venta

Prueba: `IdempotenciaTests.ReenviarElMismoFormularioDeVenta_NoDebeDuplicarLaOperacion`.

Antes de la corrección, reenviar el mismo formulario generaba otra factura. El caso original se conserva y ahora exige una única factura, un único movimiento y una sola secuencia consumida. El reenvío devuelve el comprobante existente.

La nueva comprobación de doble envío simultáneo exige además que ambas respuestas sean redirecciones correctas; ya no basta con que una falle por contención.

## Hallazgo original 2, corregido: reenvío secuencial de una nota de crédito

Prueba: `IdempotenciaTests.ReenviarElMismoFormularioDeCredito_NoDebeAcreditarDosVeces`.

Antes de la corrección, repetir el formulario de un crédito parcial emitía otra nota mientras quedaba saldo. El caso original se conserva y ahora comprueba que exista una sola nota y un solo reintegro. Ambas respuestas dirigen a la misma nota.

También se comprueban el reenvío simultáneo de crédito, anulaciones totales, correcciones de texto y notas de débito.

## Diseño aplicado y siguiente paso

Cada formulario obtiene un token de Data Protection vinculado al usuario, flujo y factura original cuando corresponde. El filtro adquiere un bloqueo SQL por clave y comprueba si ya existe su documento antes de validar el estado comercial. La clave y una huella SHA-256 de los campos de entrada se guardan con factura, secuencia y movimientos dentro de la transacción existente. Un índice único filtrado es una segunda defensa.

No se desactivó ninguna prueba ni se añadieron exclusiones o `continue-on-error`. Se agregaron 8 casos unitarios y 18 de integración a los 64 originales.

El usuario todavía debe instalar los archivos, ejecutar las pruebas en Windows y aplicar la migración con respaldo previo, siguiendo `docs/INSTALAR_IDEMPOTENCIA.md`. La migración agrega dos columnas opcionales y un índice; no modifica importes ni elimina facturas.

## Qué se verificó además

Las comprobaciones aprobadas incluyen cálculos con ITBIS incluido y no incluido, rechazo por stock y rangos inválidos sin escrituras parciales, uso de la última secuencia, protección antifalsificación, crédito parcial con y sin reintegro, anulación total en la misma fecha y en otra, corrección de texto, notas de débito, permisos HTTP y restricciones de integridad en las operaciones simultáneas incluidas en la batería.

Además se comprueban tokens ausentes, inventados o ajenos; cambios de datos después de una emisión; formularios nuevos equivalentes; recuperación tras errores de validación y persistencia; unicidad en SQL; dos hosts independientes con configuración de claves compartida; y conservación de documentos anteriores al migrar.

## Dependencias

La auditoría de paquetes detectó dos dependencias transitivas de severidad baja que ya provienen del proyecto web: `NuGet.Packaging 6.11.0` y `NuGet.Protocol 6.11.0` (aviso `GHSA-g4vj-cjjj-v7hg`). Su actualización queda como tarea separada; no se cambió el framework ni se actualizaron dependencias de negocio para instalar los tests.

El parser HTML agregado para las pruebas usa AngleSharp `1.5.0`, que incluye la corrección indicada por `GHSA-pgww-w46g-26qg`; no se entregó la versión vulnerable detectada durante la preparación.

## Límites del resultado

La compilación correcta no significa que todos los flujos estén libres de errores. Las pruebas no certifican la aplicación ante DGII, no cubren todos los escenarios de carga y no ejecutan JavaScript ni inspección visual del navegador. Los dos hosts de prueba son instancias independientes dentro de un proceso, no servidores físicos separados. La primera ejecución en GitHub y la comprobación de esta corrección en Windows siguen pendientes. Se conserva .NET 8; la revisión de soporte y actualización de plataforma debe tratarse por separado antes de producción.

## Fuentes y evidencias

- Resultados ejecutables: archivos `.trx` dentro de la carpeta `resultados` del paquete.
- Código de las pruebas: `tests/NexoFELRD.UnitTests` y `tests/NexoFELRD.IntegrationTests`.
- Guía técnica y bibliografía: `tests/README.md`.
- Microsoft. Pruebas de integración: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-8.0
- Microsoft. Estrategia de pruebas EF Core: https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy
- GitHub. Pruebas .NET en Actions: https://docs.github.com/es/actions/tutorials/build-and-test-code/net
- Auditoría de NuGet: https://github.com/advisories/GHSA-g4vj-cjjj-v7hg
- Aviso de AngleSharp: https://github.com/advisories/GHSA-pgww-w46g-26qg
