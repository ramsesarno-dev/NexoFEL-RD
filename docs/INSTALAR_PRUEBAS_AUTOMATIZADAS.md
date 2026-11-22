# Instalar las pruebas automatizadas, paso a paso

Preparado para NexoFEL RD, rama `develop`, commit base `42d651bc781a0202fa30ccf281da8446931287cd`.

## Antes de copiar

1. Detenga la aplicación en Visual Studio.
2. Guarde sus cambios pendientes con un commit o una copia de respaldo.
3. Confirme que trabaja con la versión base indicada. Si modificó después `Program.cs`, la solución o el workflow, revise los cambios antes de reemplazarlos.
4. No copie carpetas `bin`, `obj`, bases SQL ni contraseñas. El paquete solo contiene código y documentación.

## Archivos que se agregan

- `tests/NexoFELRD.UnitTests/`: proyecto y pruebas unitarias.
- `tests/NexoFELRD.IntegrationTests/`: proyecto, infraestructura y pruebas de integración.
- `docs/INSTALAR_PRUEBAS_AUTOMATIZADAS.md`: esta guía.
- `docs/RESULTADOS_PRUEBAS.md`: resultados y hallazgos de la ejecución local.

## Archivos que se actualizan

- `NexoFELRD.sln`: incorpora los dos proyectos de pruebas.
- `src/NexoFELRD.Web/Program.cs`: solo añade al final `public partial class Program { }` para poder hospedar la aplicación desde las pruebas.
- `.github/workflows/build.yml`: incorpora ejecución automática y SQL Server temporal.
- `tests/README.md`: explica seguridad, ejecución, cobertura y resultados.

## Opción A: copiar los archivos

1. Extraiga el ZIP en una carpeta aparte.
2. Entre en la carpeta `archivos` del paquete.
3. Copie su contenido a la raíz de su repositorio, donde está `NexoFELRD.sln`, conservando las subcarpetas.
4. Acepte el reemplazo únicamente de los cuatro archivos indicados arriba, si coinciden con su versión base. No sustituya todo el repositorio.
5. Reabra la solución. Debe ver `NexoFELRD.Web`, `NexoFELRD.UnitTests` y `NexoFELRD.IntegrationTests`.

## Opción B: aplicar el parche con Git

Como alternativa a copiar, sitúe `pruebas-automatizadas.patch` junto a su repositorio o use su ruta absoluta. En una terminal abierta en la raíz del repositorio:

```powershell
git apply --check RUTA_DEL_PARCHE
git apply RUTA_DEL_PARCHE
```

Sustituya `RUTA_DEL_PARCHE` por la ruta real del archivo. Si la primera orden señala conflictos, deténgase y no fuerce la aplicación del parche. Las opciones A y B son alternativas: no aplique ambas.

## Ejecutar sin tocar su base actual

Primero, abra PowerShell/Terminal en la carpeta de la solución y compile:

```powershell
dotnet restore NexoFELRD.sln --disable-parallel --property:BuildInParallel=false
dotnet build NexoFELRD.sln --configuration Release --no-restore --maxcpucount:1
dotnet test tests/NexoFELRD.UnitTests/NexoFELRD.UnitTests.csproj --configuration Release --no-build
```

Para integración con LocalDB, en esa misma ventana:

```powershell
$env:NEXOFEL_TEST_SQL_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Database=master;Integrated Security=True;Encrypt=False;TrustServerCertificate=True'
dotnet test tests/NexoFELRD.IntegrationTests/NexoFELRD.IntegrationTests.csproj --configuration Release --no-build --logger 'trx;LogFileName=integracion.trx' --results-directory TestResults/integracion
```

Esta opción requiere que LocalDB esté instalado. Si usa otra instancia local, consulte `tests/README.md` para adaptar `Server`. Mantenga `Database=master`: las pruebas generan y eliminan sus propias bases, no su base actual. No es necesario Docker en Windows para utilizar LocalDB.

## Qué resultados esperar inicialmente

- Las pruebas unitarias deben aprobarse.
- Las pruebas de integración cubren los escenarios documentados en `tests/README.md`.
- El paquete inicial detectó dos fallos de idempotencia. Si también instala la corrección de `docs/INSTALAR_IDEMPOTENCIA.md`, esos casos deben aprobarse junto con las nuevas comprobaciones; no se deben omitir para ocultar un error.
- La primera ejecución puede tardar más por la descarga de paquetes y la creación de las bases temporales.
- Si falla una prueba, revise su nombre y mensaje antes de repetir ventas manuales o cambiar su base.

Consulte `docs/RESULTADOS_PRUEBAS.md`, incluido en el paquete, para ver los resultados de la revisión local. La ejecución de GitHub Actions queda pendiente hasta que usted suba los archivos.

## Subir los cambios

Revise `git diff` y la lista de archivos. Después puede crear un commit con este mensaje:

```text
test: automatizar pruebas fiscales, inventario y permisos con SQL Server
```

Al subir a `develop`, abra la pestaña **Actions** y seleccione **Build y pruebas**. Estarán disponibles los resultados unitarios y de integración. Con la corrección de idempotencia instalada se espera una ejecución completa sin errores; cualquier resultado rojo debe investigarse, no ocultarse.

No se han hecho commits, push ni cambios de configuración en GitHub desde esta entrega. Tampoco se modificaron sus documentos fiscales guardados ni su base de desarrollo.

## Fuentes

Las referencias técnicas y el detalle de cobertura están en `tests/README.md`.
