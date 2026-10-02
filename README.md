# NexoFEL RD

Base inicial del sistema web de facturación electrónica e inventario básico para pequeñas empresas.

## Estado actual

El repositorio contiene la estructura inicial del proyecto, configuración para SQL Server e Identity y las pantallas de Dashboard, Clientes, Productos, Facturas, Inventario y Reportes. Los módulos muestran su estado inicial y quedan listos para desarrollar las operaciones de negocio.

## Tecnologías

- .NET 10 LTS
- ASP.NET Core MVC
- ASP.NET Core Identity
- Entity Framework Core
- SQL Server
- Bootstrap 5
- Git y GitHub Actions

## Requisitos

- Visual Studio 2026 con la carga de trabajo ASP.NET y desarrollo web
- .NET 10 SDK
- SQL Server 2022 o SQL Server Express LocalDB
- Git

## Primer inicio

1. Abrir `NexoFELRD.sln`.
2. Revisar la cadena `DefaultConnection` en `appsettings.Development.json`.
3. Restaurar paquetes NuGet.
4. Abrir la Consola del Administrador de Paquetes.
5. Ejecutar `Add-Migration InitialCreate`.
6. Ejecutar `Update-Database`.
7. Iniciar `NexoFELRD.Web`.

También puede utilizarse la terminal:

```bash
dotnet restore
dotnet ef migrations add InitialCreate --project src/NexoFELRD.Web
dotnet ef database update --project src/NexoFELRD.Web
dotnet run --project src/NexoFELRD.Web
```

## Flujo de ramas

- `main`: versión estable y demostrable.
- `develop`: integración del trabajo del equipo.
- `feature/nombre-breve`: desarrollo de cada módulo o tarea.

Cada cambio debe entrar a `develop` mediante una solicitud de cambios revisada por otro integrante.

## Documentación

- `docs/ENTORNO.md`: preparación del entorno.
- `docs/ESTRUCTURA.md`: organización de carpetas y responsabilidades.
- `docs/ROADMAP.md`: orden técnico de implementación.
