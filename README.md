# NexoFEL RD

Sistema académico de facturación e inventario básico para pequeñas empresas.

## Estado actual

Incluye autenticación con roles, clientes, productos, facturas E31/E32, notas E33/E34, rangos e-NCF, inventario, dashboard, reportes y pruebas automatizadas con SQL Server. Los comprobantes se pueden descargar en PDF con un QR de consulta interna que requiere sesión.

El proyecto no incorpora firma, envío ni aceptación de documentos ante DGII. El QR interno no es un mecanismo de validación oficial.

## Tecnologías

- .NET 8 LTS
- ASP.NET Core MVC
- ASP.NET Core Identity
- Entity Framework Core
- SQL Server
- Bootstrap 5
- Git y GitHub Actions

## Requisitos

- Visual Studio 2022 versión 17.8 o superior con la carga de trabajo ASP.NET y desarrollo web
- .NET 8 SDK
- SQL Server 2022 o SQL Server Express LocalDB
- Git

## Primer inicio

1. Abrir `NexoFELRD.sln`.
2. Configurar `ConnectionStrings:DefaultConnection`, `SeedAdmin:Email` y `SeedAdmin:Password` en User Secrets para desarrollo.
3. Restaurar paquetes NuGet.
4. Abrir la Consola del Administrador de Paquetes.
5. La solución ya contiene migraciones: no generar otra `InitialCreate`.
6. Para una instalación nueva, aplicar las migraciones existentes con `Update-Database`. El arranque en Development también las aplica. No recrear una base existente.
7. Iniciar `NexoFELRD.Web`.

También puede utilizarse la terminal:

```bash
dotnet restore
dotnet ef database update --project src/NexoFELRD.Web
dotnet run --project src/NexoFELRD.Web
```

## Flujo de ramas

- `main`: versión estable y demostrable.
- `develop`: integración del trabajo del equipo.
- `feature/nombre-breve`: desarrollo de cada módulo o tarea.

Cada cambio debe entrar a `develop` mediante una solicitud de cambios revisada por otro integrante.

## Documentación

- `docs/PDF_Y_QR.md`: descarga de facturas/notas, configuración del emisor y QR de consulta interna.
- `docs/RESULTADOS_PDF_QR.md`: alcance y resultados de las pruebas del nuevo módulo.
- `docs/ENTORNO.md`: preparación del entorno.
- `docs/ESTRUCTURA.md`: organización de carpetas y responsabilidades.
- `docs/ROADMAP.md`: orden técnico de implementación.
