# Feature: Stock Insumos y Politica Min-Max Dinamica
Branch: feat/stock-insumos
Tracking: odd/tasks/stock-insumos-minmax.md

## Specs
- S1: "hay alguna manera de que cuando actualizo el nombre de un insumo, los servicios que lo utilizan se actualicen tambien para referirse al mismo insumo ? Porque le di a editar el servicio y seguia saliendo el nombre viejo. Deberia usar el id en su lugar ? O implementar un trigger de actualizacion en segundo plano o algo asi ?"
- S2: "actualmente el campo de Cantidad de reposicion es un eslabon debil en el diseño, ya que es un valor fijo, que para empezar esta mal porque me deja poner un valor menor al valor definido en stock minimo, y esto ocasiona que pueda darse el caso de que la orden de compra automatica pida menos de lo que se necesita, y si a eso le sumamos que por ejemplo es un insumo en uso, podria ocasionar que el reabastecimiento sea menor incluso a lo que se se esta reservando. Por ejemplo tengo un liquido, 10l de base, su stock minimo es 5l y su cantidad de reposicion es 2l, estoy realizando un lavado que requiere 1l pero por culpa de un accidente se cae por completo el bidon de detergente o liquido, entonces el emplado registra que finalizo la etapa con el desvio correspondiente al liquido, ahi automaticamente entra en stock critico de golpe, pero la compra automatica no alcanzaria para alcanzar el stock minimo requerido por el insumo, y si supongamos hubieran otros servicios que Tambien usaban ese liquido, menos aun, porque ni alcanzaria el minimo ni sumaria lo reservado. Que se puede hacer con ese campo, que propones ?"
- S3: "valida que quede funcionando bien y contemple la migracion para los insumos que ya existen"
- S4: "que opinas de que el stock minimo no pueda ser 0 ? porque actualmente tecnicamente podes registrar un insumo con stock minimo en 0, stock maximo en 0 y cantidad de reposicion en 0, entonces apenas se crea esta con stock critico, cosa que tecnicamente no deberia entrar en alerta porque esta cumpliendo la igualdad pero bueno, igual funcionalmente para que vas a registrar un insumo cuya canitdad puede ser 0."
- S5: "hay alguna forma de que la validacion de que el stock maximo debe ser mayor al stock minimo ocurra antes de darle a guardar asi no pierdo los cambios ? Igual preferiria que el javascript lo impida o que aproposito si intenta poner un valor mas grande, crezca el stock minimo automaticamente."
- S6: "las herramientas o insumos cuya unidad de medida es la unidad, me deja guardar decimal, no un entero, hay que corregir eso tanto para ahi como para cuando se agrega como insumo a un lavado (que no se si lo hace pero no creo )."
- S7: "visualmente, me gustaria que el formulario de Registrar Insumo entrase ya en 2 filas, quizas achicando los desplegables que son enormes para la unidad, stock minimo, stock maximo y cantidad de reposicion, entonces como se ve en la imagen adjuntada a continuacion, el selector de proveedores entra en el rectangulo rojo. Por cierto esos campos nuevos no tienen ni placeholder ni descripcion alguna de contexto."

## Tasks
- T1 (S1): Sincronizar nombres de Insumo por InsumoId en Servicios al editar | route: inline | commit: 188f510
- T2 (S2, S3): Expandir modelo Insumo con StockMaximo y fallback retrocompatible | route: inline | commit: 188f510
- T3 (S2, S3): Regla de negocio en InsumosService y cálculo dinámico de reposición | route: inline | commit: 188f510
- T4 (S1, S2): Vistas y scripts UI de Insumo y Servicio con política Min-Max | route: inline | commit: 188f510
- T5 (S1, S2, S3): Verificación con tests unitarios y de integración | route: inline | commit: 188f510
- T6 (S4, S6): StockMinimo estricto > 0 y restricción de enteros en unidades en backend | route: inline | commit: 470047c
- T7 (S4, S5, S6): Validación en tiempo real, auto-grow e integer steps en JS | route: inline | commit: 470047c
- T8 (S7): Reorganización visual de `_InsumoForm.cshtml` en 2 filas y proveedores en cols 3-4 con placeholders y ayudas | route: inline | commit: 470047c
- T9 (S4, S5, S6, S7): Pruebas automatizadas en C# y Node.js para refinamiento Min-Max y enteros | route: inline | commit: 470047c

## Log
- L1: User requested insumo name updating in services, min-max replenishment policy to avoid short orders during deviations/reservations, and backward-compatible migration for existing insumo records.
- L2: User requested StockMinimo > 0, real-time validation and auto-growth in JS preventing submission with StockMaximo <= StockMinimo, integer-only constraint for countable units (form, adjustment, wash and recipe), and 2-row layout with provider selector in cols 3-4 and toggle in header.
