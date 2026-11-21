# Pruebas automatizadas de NexoFEL RD

La solución incluye dos proyectos. Las pruebas no envían documentos a DGII ni usan datos del negocio.

| Proyecto | Alcance |
| --- | --- |
| `NexoFELRD.UnitTests` | Clasificación de comprobantes, signos y reglas de inclusión fiscal. No necesita SQL Server. |
| `NexoFELRD.IntegrationTests` | Solicitudes HTTP a la aplicación, login real de Identity, antifalsificación, permisos, migraciones, transacciones, inventario, secuencias y reportes con SQL Server. |

## 1. Seguridad de los datos

- Integración lee exclusivamente `NEXOFEL_TEST_SQL_CONNECTION`; no utiliza la conexión de `appsettings` como alternativa.
- La conexión debe apuntar a un SQL Server **local**, con `Database=master`, sin `AttachDBFilename`.
- Se rechaza explícitamente una conexión a `NexoFELRD_Dev` o a cualquier otra base nombrada por el usuario.
- Cada caso crea una base `NexoFELRD_Tests_` seguida de un GUID aleatorio y aplica las migraciones de la aplicación.
- Al finalizar, elimina únicamente la base creada por ese caso. Antes de eliminarla verifica su nombre y su cadena de conexión.
- `master` se usa para conectarse al servidor y administrar las bases temporales, no para guardar entidades de la aplicación.
- El usuario de SQL necesita permisos para crear y eliminar **bases temporales**. No cambie los permisos de un servidor ajeno para ejecutar las pruebas.
- Si se interrumpe el proceso de forma abrupta, puede quedar una base temporal. Su eliminación posterior debe revisar el nombre completo; nunca elimine bases mediante comodines.

## 2. Pruebas unitarias, sin base de datos

En una terminal abierta en la carpeta que contiene `NexoFELRD.sln`:

```powershell
dotnet test tests/NexoFELRD.UnitTests/NexoFELRD.UnitTests.csproj --configuration Release
```

## 3. Integración local en Windows, con LocalDB

Requisitos: SDK .NET 8, SQL Server Express LocalDB instalado y su instancia disponible. No es necesario Docker para esta opción.

En PowerShell, en la raíz del repositorio:

```powershell
$env:NEXOFEL_TEST_SQL_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True'
dotnet test tests/NexoFELRD.IntegrationTests/NexoFELRD.IntegrationTests.csproj --configuration Release --logger 'trx;LogFileName=integracion.trx' --results-directory TestResults/integracion
```

Si utiliza SQL Server Express en otra instancia local, sustituya únicamente `Server`, por ejemplo `Server=localhost\SQLEXPRESS`. Mantenga `Database=master`. Si la conexión falla, confirme el nombre de su instancia antes de continuar.

No ejecute `Add-Migration`, `Update-Database`, `EnsureDeleted` ni scripts manuales sobre su base de desarrollo para preparar estas pruebas.

## 4. Qué valida la batería de integración

- Venta de seis unidades de RD$27.00 con 16 % de ITBIS incluido: base RD$139.66, ITBIS RD$22.34 y total RD$162.00.
- Cálculos de 16 % y 18 %, tanto incluidos como no incluidos en el precio.
- Descuento del stock, movimiento de salida y consumo exacto de una secuencia.
- Rechazo por existencias insuficientes, rango agotado, vencido, inactivo o inexistente, sin escrituras parciales.
- Uso de la última secuencia disponible y posterior bloqueo del rango.
- Rechazo de POST sin token antifalsificación.
- Crédito parcial con y sin reintegro físico; el reporte toma cantidades de los movimientos reales.
- Rechazo de cantidades superiores al saldo después de una acreditación parcial.
- Anulación total y mantenimiento del efecto histórico de la factura original, incluso si la nota tiene otra fecha.
- Corrección de texto sin efecto monetario ni reintegros; rechazo si se intenta cambiar cantidades.
- Nota de débito con aumento monetario y sin salida ficticia de mercancía.
- Permisos de Administrador, Facturador y visitante; también se prueba un POST administrativo directo.
- Dos ventas por la última unidad, dos créditos sobre el mismo saldo y conservación de secuencias únicas.
- Reenvío del mismo formulario de venta o crédito, secuencial y simultáneo, para detectar duplicados.
- Reenvío de notas de débito, anulaciones totales y correcciones de texto.
- Claves ligadas al usuario, flujo y factura; rechazo de solicitudes manipuladas y cambios en una operación ya procesada.
- Formularios nuevos legítimos, reintentos después de validación fallida o rollback, índice único, dos hosts independientes y conservación de documentos al migrar.

Las pruebas de concurrencia se envían con clientes/solicitudes independientes y una salida coordinada. Son comprobaciones de integridad bajo los escenarios incluidos, no una demostración de ausencia de todas las condiciones de carrera. La prueba de secuencias permite un rechazo controlado por contención y exige que no deje escrituras parciales; no mide rendimiento ni garantiza que ambas ventas legítimas se confirmen sin reintento.

## 5. GitHub Actions

El flujo `.github/workflows/build.yml` ejecuta:

1. Restauración, compilación y pruebas unitarias.
2. SQL Server 2022 Developer en un contenedor exclusivo del runner de integración.
3. Pruebas de integración completas.
4. Publicación de los resultados `.trx` como artefactos, también cuando falle alguna prueba.
5. Parada del contenedor temporal.

La contraseña SQL del runner se genera en cada ejecución, se enmascara en los logs y no necesita un secreto del repositorio. No es una contraseña de su SQL Server ni se envía la conexión de desarrollo a GitHub. La edición Developer se utiliza únicamente para desarrollo/pruebas; la ejecución del contenedor requiere aceptar los términos de Microsoft, indicados por `ACCEPT_EULA=Y`.

El flujo se activa al hacer push o abrir/actualizar un pull request hacia `develop`/`main`; también admite ejecución manual cuando GitHub muestre `Run workflow`. No se ha publicado ni ejecutado este cambio en su cuenta desde este paquete.

Si desea impedir fusiones con pruebas fallidas, configure las comprobaciones obligatorias de la rama en GitHub. Un workflow rojo por sí solo no establece esa regla.

## 6. Interpretación de resultados y hallazgos conocidos

En el commit base `42d651b`, las pruebas de reenvío secuencial detectaron duplicación de ventas y notas de crédito. La corrección posterior de idempotencia resuelve esos dos casos manteniendo sus comprobaciones; no se eliminaron, omitieron ni suavizaron para obtener una ejecución verde.

Cada formulario obtiene un token protegido por Data Protection. Un filtro adquiere un bloqueo SQL por clave y reconoce un comprobante ya emitido antes de consultar el estado comercial del original. La clave y la huella de entrada se guardan junto al comprobante dentro de su transacción. Un índice único impide claves repetidas. Dos formularios nuevos siguen siendo operaciones distintas.

`Categoria=Idempotencia` etiqueta esos casos para diagnosticarlos individualmente; el workflow ejecuta toda la batería y no excluye esta categoría.

```powershell
dotnet test tests/NexoFELRD.IntegrationTests/NexoFELRD.IntegrationTests.csproj --configuration Release --filter 'Categoria=Idempotencia'
```

Los fallos de conexión, autenticación o infraestructura también hacen fallar las pruebas; no se convierten en pruebas omitidas.

## 7. Limitaciones

- No son pruebas de navegador: no ejecutan JavaScript, no comprueban el aspecto visual ni generan capturas. Para eso se puede añadir posteriormente una batería pequeña de Playwright.
- No evalúan certificación, firma o comunicación con DGII.
- Los usuarios, contraseñas y productos del entorno de pruebas son simulados y solo se crean en sus bases temporales.
- La batería se corresponde con los flujos actualmente implementados, no con todos los comprobantes declarados en el enum.
- El alojamiento de tests sigue utilizando `public partial class Program { }`. La corrección posterior sí modifica la emisión y agrega la migración `20261121212033_ProtegerEmisionIdempotente`; consulte `docs/INSTALAR_IDEMPOTENCIA.md` antes de actualizar su base de desarrollo.
- Los dos hosts del test son instancias independientes dentro de un proceso de pruebas; no equivalen a una prueba de carga distribuida en producción.

## Referencias

- Microsoft. Pruebas de integración en ASP.NET Core: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-8.0
- Microsoft. Elección de una estrategia de pruebas de EF Core: https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy
- xUnit.net. Introducción a xUnit.net v2: https://xunit.net/docs/getting-started/v2/getting-started
- GitHub. Compilar y probar .NET: https://docs.github.com/es/actions/tutorials/build-and-test-code/net
- Microsoft. Configuración de SQL Server mediante variables: https://learn.microsoft.com/en-us/sql/linux/configure/environment-variables?view=sql-server-ver16
- Microsoft. Bloqueos SQL Server: https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql?view=sql-server-ver16
- Microsoft. Data Protection, aislamiento por propósito: https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/purpose-strings
- Microsoft. Aplicación de migraciones EF Core: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
