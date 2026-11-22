# Configuración del entorno

## Herramientas requeridas

1. Visual Studio 2022 versión 17.8 o superior con ASP.NET y desarrollo web.
2. .NET 8 SDK.
3. SQL Server 2022 o SQL Server Express LocalDB.
4. SQL Server Management Studio.
5. Git.

## Configuración local

La configuración inicial contempla LocalDB. Cada integrante debe configurar su conexión local en User Secrets (`ConnectionStrings:DefaultConnection`), sin almacenar credenciales reales en el repositorio. El módulo E45 no cambia esa configuración.

## Base de datos

La solución ya contiene las migraciones y los roles `Administrador` y `Facturador`. Para una instalación nueva, aplicar las migraciones existentes; no generar otra migración inicial. No eliminar ni recrear una base existente para instalar una actualización. El módulo E45 no modifica el modelo persistido ni necesita una migración nueva.

## Comprobaciones iniciales

- La solución abre sin proyectos faltantes.
- Los paquetes NuGet se restauran correctamente.
- La aplicación puede conectarse a SQL Server.
- Una ruta protegida redirige al inicio de sesión.
- La navegación muestra todos los módulos iniciales.
