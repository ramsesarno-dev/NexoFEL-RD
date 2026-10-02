# Configuración del entorno

## Herramientas requeridas

1. Visual Studio 2026 con ASP.NET y desarrollo web.
2. .NET 10 SDK.
3. SQL Server 2022 o SQL Server Express LocalDB.
4. SQL Server Management Studio.
5. Git.

## Configuración local

La cadena de desarrollo usa LocalDB. Si el equipo utiliza una instancia diferente, cada integrante debe modificar únicamente `appsettings.Development.json` y no almacenar credenciales reales en el repositorio.

## Base de datos

Después de restaurar los paquetes, se debe crear la migración inicial y actualizar la base de datos. Los roles `Administrador` y `Facturador` están definidos como datos iniciales en `ApplicationDbContext` y serán incluidos en la primera migración.

## Comprobaciones iniciales

- La solución abre sin proyectos faltantes.
- Los paquetes NuGet se restauran correctamente.
- La aplicación puede conectarse a SQL Server.
- Una ruta protegida redirige al inicio de sesión.
- La navegación muestra todos los módulos iniciales.
