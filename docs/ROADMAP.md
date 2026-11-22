# Roadmap técnico

## Etapa 1 Base del proyecto

- Crear repositorio, solución y proyecto MVC.
- Configurar SQL Server, Entity Framework Core e Identity.
- Crear navegación y pantallas iniciales.

## Etapa 2 Seguridad y catálogos

- Completar login, roles y usuario administrador.
- Implementar clientes y productos.
- Agregar validaciones y mensajes de resultado.

## Etapa 3 Facturación e inventario

- Crear factura y detalle.
- Calcular subtotal, ITBIS y total en el servidor.
- Confirmar la venta dentro de una transacción.
- Descontar existencias y registrar movimientos.

## Etapa 4 Documento digital y reportes

- Generar PDF y código QR.
- Consultar facturas por fecha y estado.
- Completar dashboard y reportes básicos.

## Etapa 5 Calidad y entrega

- Agregar pruebas unitarias y de integración.
- Corregir hallazgos de seguridad y experiencia.
- Preparar datos de demostración, manual y despliegue.

## Ampliación de los tipos electrónicos

El catálogo del modelo contiene los diez códigos, pero eso no significa que todos tengan emisión implementada.

- Implementados: E31, E32, E33, E34 y E45. Las notas se crean desde el documento original.
- Próximo bloque de ventas especiales: E44 (regímenes especiales) y E46 (exportaciones).
- Bloque posterior con flujos propios: E41 (compras), E43 (gastos menores) y E47 (pagos al exterior).
- Cada incorporación debe cubrir validación del servidor, secuencias, efecto sobre inventario cuando corresponda, reportes, PDF, idempotencia y pruebas.
- La firma, transmisión, recepción y aceptación ante DGII constituyen un alcance diferente. No están implementadas por habilitar estos tipos en el prototipo.
