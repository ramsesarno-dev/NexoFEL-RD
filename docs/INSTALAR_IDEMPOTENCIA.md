# Corrección de envíos repetidos: instalación paso a paso

Proyecto: NexoFEL RD. Preparado el 21 de noviembre de 2026.

Este paquete complementa las pruebas que ya instalaste. Está basado en `develop`, commit `42d651bc781a0202fa30ccf281da8446931287cd`, más aquel paquete de pruebas. No es un proyecto nuevo ni debes copiarlo encima de otra versión sin revisar las diferencias.

## Qué se corrige

El mismo formulario de una venta o nota podía procesarse dos veces. Ahora cada formulario contiene una clave protegida. El servidor reconoce una solicitud que ya finalizó y devuelve su comprobante existente, sin consumir otra secuencia ni mover inventario otra vez.

Se protegen ventas E31/E32, notas de crédito E34 y notas de débito E33. Se conservan la protección antifalsificación y los permisos por roles.

## 1. Respalda antes de copiar

1. Detén la aplicación y cierra Visual Studio.
2. Guarda tus cambios actuales con un commit local o una copia de la carpeta del proyecto fuera de ella. No publiques contraseñas ni secretos.
3. Antes de actualizar la base de desarrollo, realiza también una copia de seguridad de esa base con tu herramienta de SQL Server. Una copia del código no respalda los datos.
4. Si modificaste por tu cuenta archivos de `archivos-modificados.txt` después del paquete anterior, compara e integra esos cambios antes de reemplazarlos.

No borres la base, tablas, carpeta `Migrations` ni migraciones anteriores.

## 2. Copia el contenido de archivos

Descomprime `NexoFELRD_Correccion_Idempotencia.zip`.

Dentro de `archivos` hay tres carpetas: `src`, `tests` y `docs`. Copia esas tres carpetas a la raíz de tu repositorio original:

```text
C:\Users\arnor\Downloads\NexoFEL_RD_Repositorio_NET8\NexoFEL-RD
```

Es la carpeta que contiene `NexoFELRD.sln`, `.git`, `src` y `tests`.

No copies la carpeta contenedora `archivos`. Combina sus subcarpetas con las existentes. Si ya respaldaste y no hay cambios tuyos nuevos que integrar, elige **Reemplazar los archivos en el destino** cuando Windows lo pregunte.

El paquete no incluye `.vs`, `.git`, `bin`, `obj`, `appsettings`, cadenas de conexión ni contraseñas. No reemplaza la solución ni reinstala los proyectos de pruebas.

Abre la solución original y comprueba que aparecen:

- `src/NexoFELRD.Web/Filters/EmisionIdempotenteFilter.cs`.
- `src/NexoFELRD.Web/Services/ProteccionEmision.cs`.
- La migración `20261121212033_ProtegerEmisionIdempotente.cs` y su `.Designer.cs`.
- `tests/NexoFELRD.UnitTests/ProteccionEmisionTests.cs`.
- Las versiones actualizadas de los demás archivos del paquete.

No pulses F5 todavía: en Development el programa aplica migraciones al arrancar. Primero ejecuta las pruebas aisladas siguientes.

## 3. Compila y prueba sin modificar tu base de desarrollo

Abre **Ver > Terminal**, selecciona **PowerShell para desarrolladores** y sitúate en la raíz del repositorio original. Este paso no utiliza la Consola del Administrador de paquetes.

Ejecuta estos comandos, uno por uno:

```powershell
dotnet restore NexoFELRD.sln --disable-parallel --property:BuildInParallel=false
```

```powershell
dotnet build NexoFELRD.sln --configuration Release --no-restore --maxcpucount:1
```

```powershell
dotnet test tests/NexoFELRD.UnitTests/NexoFELRD.UnitTests.csproj --configuration Release --no-build --logger 'trx;LogFileName=unitarias.trx' --results-directory TestResults/unitarias
```

Se esperan **25 pruebas unitarias aprobadas**, ninguna fallida ni omitida.

Después inicia la instancia ya comprobada en tu equipo:

```powershell
sqllocaldb start mssqllocaldb
```

En esa misma ventana de PowerShell:

```powershell
$env:NEXOFEL_TEST_SQL_CONNECTION = 'Server=(localdb)\mssqllocaldb;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True'
```

```powershell
dotnet test tests/NexoFELRD.IntegrationTests/NexoFELRD.IntegrationTests.csproj --configuration Release --no-build --logger 'trx;LogFileName=integracion.trx' --results-directory TestResults/integracion
```

Se esperan **65 pruebas de integración aprobadas**, ninguna fallida ni omitida. Hay más casos porque se agregaron comprobaciones; no se borraron las anteriores.

Los tests utilizan bases temporales `NexoFELRD_Tests_...`, aplican sus migraciones y después las eliminan. Mantén `Database=master` en esta variable: no pongas el nombre de tu base de trabajo. La variable solo la utiliza la infraestructura de tests; no sustituye `DefaultConnection` de la aplicación.

Si aparece un fallo, conserva la salida y revisa su nombre antes de continuar. No desactives pruebas ni cambies sus expectativas para obtener un resultado verde.

## 4. Aplica la migración a tu base de desarrollo

Hazlo después de respaldar la base y aprobar las pruebas.

1. Comprueba, sin publicar su contenido, que `ConnectionStrings:DefaultConnection` apunta a tu base de desarrollo habitual, no a `master` ni a una base de tests. Conserva tu configuración y User Secrets existentes.
2. Marca **NexoFELRD.Web** como proyecto de inicio en Visual Studio.
3. Abre **Herramientas > Administrador de paquetes NuGet > Consola del Administrador de paquetes**.
4. Ejecuta el comando siguiente en esa consola, no en PowerShell para desarrolladores:

```powershell
Update-Database -Migration 20261121212033_ProtegerEmisionIdempotente -Context ApplicationDbContext -Project NexoFELRD.Web -StartupProject NexoFELRD.Web
```

La migración ya está incluida: **no ejecutes `Add-Migration` otra vez**. Tampoco uses `Drop-Database`, `Remove-Migration` ni un retroceso de migraciones como parte de esta instalación.

Solo se agregan dos columnas opcionales (`ClaveOperacion` y `HuellaSolicitud`) y un índice único filtrado. Las facturas anteriores quedan con esos campos nulos; no se recalculan importes, no se reinician secuencias y no se eliminan documentos. La prueba de compatibilidad comprueba la conservación de datos simulados al migrar.

## 5. Comprueba la aplicación

1. Inicia normalmente el proyecto. Cierra cualquier formulario abierto antes de actualizar y abre uno nuevo; los formularios antiguos no contienen el nuevo token.
2. En datos de prueba, registra una venta y comprueba inventario y e-NCF.
3. Reenviar el mismo formulario procesado debe dirigir al comprobante existente, sin generar otro e-NCF ni descontar otra unidad. Las pruebas HTTP verifican este comportamiento sin depender del navegador.
4. Abrir **Nueva factura** nuevamente sí genera otra clave y permite otra venta legítima, aunque coincidan producto y cantidad.
5. Las notas parciales nuevas deben seguir descontando únicamente su importe y reintegrando la cantidad seleccionada cuando corresponda.

## Comportamiento esperado

| Situación | Resultado |
| --- | --- |
| Mismo formulario y mismos datos, ya procesados | Comprobante existente; ninguna escritura adicional |
| Formulario ya procesado, con otros datos | HTTP 409; abrir otro formulario para otra operación |
| Token ausente, alterado, de otro usuario, flujo o factura | HTTP 400; no se registra la operación |
| Error de validación o transacción revertida | No se consume la clave; permite corregir o reintentar |
| Formulario nuevo | Operación nueva, sujeta a todas las validaciones normales |
| Repetición mientras la primera solicitud sigue ejecutándose | Espera hasta 10 segundos; si no obtiene el bloqueo, HTTP 409 y posibilidad de reenviar el mismo formulario |

El bloqueo se realiza en SQL Server y se libera al terminar la acción. El índice único es una segunda defensa. No se utiliza una lista de claves en memoria ni se sustituye la protección por deshabilitar un botón.

## Alcance y precauciones

- Los documentos anteriores no tienen clave; este cambio no identifica ni elimina duplicados históricos automáticamente. Tampoco autoriza borrar comprobantes fiscales.
- Dos pestañas obtenidas mediante dos GET independientes generan claves diferentes. La solución evita el reenvío de una misma operación, no decide si dos ventas equivalentes eran intencionales.
- Conserva las claves de ASP.NET Core Data Protection. Si despliegas varias instancias, deben compartir el almacén de claves y la identidad de aplicación. La prueba de dos hosts comparte esa configuración local, pero no sustituye revisar el despliegue real.
- No cambia tasas, redondeos, importes históricos ni reglas de reportes.
- No agrega paquetes ni actualiza el framework. Los pendientes de mantenimiento y soporte de .NET deben tratarse por separado antes de producción.
- No certifica integración con DGII ni ejecuta JavaScript del navegador.
- No se hizo commit, push ni se ejecutó GitHub Actions en tu cuenta desde esta entrega.

Cuando las comprobaciones de tu equipo aprueben, puedes revisar el diff y utilizar este mensaje para el commit:

```text
fix: impedir duplicados en facturas y notas con idempotencia persistente
```

El workflow ya instalado ejecutará también los casos adicionales cuando subas los cambios a `develop`.

## Webgrafía y referencias técnicas

- Microsoft. Filtros de ASP.NET Core: https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/filters
- Microsoft. Data Protection, aislamiento por propósitos: https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/purpose-strings
- Microsoft. `sp_getapplock`: https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql?view=sql-server-ver16
- Microsoft. `sp_releaseapplock`: https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-releaseapplock-transact-sql?view=sql-server-ver16
- Microsoft. Índices de EF Core: https://learn.microsoft.com/en-us/ef/core/modeling/indexes
- Microsoft. Aplicación de migraciones: https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying
- Evidencia propia: código y archivos `.trx` de este paquete. Las fuentes describen las herramientas; los resultados pertenecen a las ejecuciones del proyecto.
