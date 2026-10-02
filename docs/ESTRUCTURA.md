# Estructura inicial

```text
NexoFEL-RD
├── .github
│   └── workflows
├── docs
├── src
│   └── NexoFELRD.Web
│       ├── Controllers
│       ├── Data
│       ├── Models
│       ├── Properties
│       ├── Views
│       └── wwwroot
└── tests
```

## Responsabilidades

- `Controllers`: reciben solicitudes y coordinan cada pantalla.
- `Data`: contexto de Entity Framework Core y configuración de persistencia.
- `Models`: entidades y enumeraciones del negocio.
- `Views`: páginas Razor organizadas por módulo.
- `wwwroot`: estilos, scripts e imágenes públicas.
- `docs`: decisiones técnicas y guías para el equipo.
- `tests`: ubicación reservada para pruebas automatizadas.

La primera versión utiliza un monolito modular. Todos los módulos se ejecutan en la misma aplicación, pero mantienen carpetas y responsabilidades separadas.
