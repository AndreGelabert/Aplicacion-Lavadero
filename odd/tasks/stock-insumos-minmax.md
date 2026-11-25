# Feature: Stock Insumos y Politica Min-Max Dinamica
Branch: feat/stock-insumos
Tracking: odd/tasks/stock-insumos-minmax.md

## Specs
- S1: "hay alguna manera de que cuando actualizo el nombre de un insumo, los servicios que lo utilizan se actualicen tambien para referirse al mismo insumo ? Porque le di a editar el servicio y seguia saliendo el nombre viejo. Deberia usar el id en su lugar ? O implementar un trigger de actualizacion en segundo plano o algo asi ?"
- S2: "actualmente el campo de Cantidad de reposicion es un eslabon debil en el diseño, ya que es un valor fijo, que para empezar esta mal porque me deja poner un valor menor al valor definido en stock minimo, y esto ocasiona que pueda darse el caso de que la orden de compra automatica pida menos de lo que se necesita, y si a eso le sumamos que por ejemplo es un insumo en uso, podria ocasionar que el reabastecimiento sea menor incluso a lo que se se esta reservando. Por ejemplo tengo un liquido, 10l de base, su stock minimo es 5l y su cantidad de reposicion es 2l, estoy realizando un lavado que requiere 1l pero por culpa de un accidente se cae por completo el bidon de detergente o liquido, entonces el emplado registra que finalizo la etapa con el desvio correspondiente al liquido, ahi automaticamente entra en stock critico de golpe, pero la compra automatica no alcanzaria para alcanzar el stock minimo requerido por el insumo, y si supongamos hubieran otros servicios que Tambien usaban ese liquido, menos aun, porque ni alcanzaria el minimo ni sumaria lo reservado. Que se puede hacer con ese campo, que propones ?"
- S3: "valida que quede funcionando bien y contemple la migracion para los insumos que ya existen"

## Tasks
- T1 (S1): Sincronizar nombres de Insumo por InsumoId en Servicios al editar | route: inline | commit: 2a4de5c
- T2 (S2, S3): Expandir modelo Insumo con StockMaximo y fallback retrocompatible | route: inline | commit: 2a4de5c
- T3 (S2, S3): Regla de negocio en InsumosService y cálculo dinámico de reposición | route: inline | commit: 2a4de5c
- T4 (S1, S2): Vistas y scripts UI de Insumo y Servicio con política Min-Max | route: inline | commit: 2a4de5c
- T5 (S1, S2, S3): Verificación con tests unitarios y de integración | route: inline | commit: 2a4de5c

## Log
- L1: User requested insumo name updating in services, min-max replenishment policy to avoid short orders during deviations/reservations, and backward-compatible migration for existing insumo records.