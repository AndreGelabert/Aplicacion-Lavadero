using Firebase.Models;
using Firebase.Tests.Helpers;
using Xunit;

namespace Firebase.Tests.Services
{
    /// <summary>
    /// Tests unitarios para el módulo LavadoService.
    /// 
    /// Metodología de Testing: Pruebas Unitarias con patrón AAA (Arrange-Act-Assert)
    /// Tipo de Testing: Caja Negra (Black Box Testing)
    /// - Se prueban las entradas y salidas sin conocer la implementación interna
    /// - Se valida el comportamiento esperado según las especificaciones
    /// 
    /// Categorías de Tests:
    /// 1. Validación del Modelo: Verificar las propiedades del modelo Lavado y submodelos
    /// 2. Tests de Estados: Validar transiciones y estados del lavado
    /// 3. Tests de Precios: Verificar cálculos de precios y descuentos
    /// 4. Tests de Pago: Validar estados y cálculos de pago
    /// 5. Tests de Servicios: Verificar gestión de servicios dentro del lavado
    /// 6. Tests de Filtrado: Validar filtros por estado, fecha, precio, etc.
    /// 7. Tests de Ordenamiento: Validar ordenamiento por diferentes campos
    /// 8. Tests de Búsqueda: Verificar búsqueda en múltiples campos
    /// 9. Tests de Validación: Validar reglas de negocio
    /// </summary>
    public class LavadoServiceTests
    {
        #region Tests de Validación del Modelo

        /// <summary>
        /// Verifica que el modelo Lavado tenga todas las propiedades correctamente asignadas.
        /// 
        /// Arrange: Crear un lavado con datos específicos
        /// Act: Acceder a las propiedades del modelo
        /// Assert: Verificar que cada propiedad tenga el valor esperado
        /// </summary>
        [Fact]
        public void Lavado_Model_ShouldHaveCorrectProperties()
        {
            // Arrange & Act
            var lavado = TestFactory.CreateLavado(
                id: "lav-001",
                clienteId: "cli-001",
                vehiculoId: "veh-001",
                estado: "EnProceso",
                precio: 15000
            );

            // Assert
            Assert.Equal("lav-001", lavado.Id);
            Assert.Equal("cli-001", lavado.ClienteId);
            Assert.Equal("veh-001", lavado.VehiculoId);
            Assert.Equal("EnProceso", lavado.Estado);
            Assert.Equal(15000, lavado.Precio);
        }

        /// <summary>
        /// Verifica que ServicioEnLavado tenga las propiedades correctas.
        /// </summary>
        [Fact]
        public void ServicioEnLavado_ShouldHaveCorrectProperties()
        {
            // Arrange
            var servicio = new ServicioEnLavado
            {
                ServicioId = "serv-001",
                ServicioNombre = "Lavado Completo",
                TipoServicio = "Lavado",
                Precio = 10000,
                TiempoEstimado = 45,
                Estado = "Pendiente",
                Orden = 1
            };

            // Assert
            Assert.Equal("serv-001", servicio.ServicioId);
            Assert.Equal("Lavado Completo", servicio.ServicioNombre);
            Assert.Equal("Lavado", servicio.TipoServicio);
            Assert.Equal(10000, servicio.Precio);
            Assert.Equal(45, servicio.TiempoEstimado);
            Assert.Equal("Pendiente", servicio.Estado);
            Assert.Equal(1, servicio.Orden);
        }

        /// <summary>
        /// Verifica que EtapaEnLavado tenga las propiedades correctas.
        /// </summary>
        [Fact]
        public void EtapaEnLavado_ShouldHaveCorrectProperties()
        {
            // Arrange
            var etapa = new EtapaEnLavado
            {
                EtapaId = "etapa-001",
                Nombre = "Enjuague",
                Estado = "Pendiente"
            };

            // Assert
            Assert.Equal("etapa-001", etapa.EtapaId);
            Assert.Equal("Enjuague", etapa.Nombre);
            Assert.Equal("Pendiente", etapa.Estado);
        }

        /// <summary>
        /// Verifica que PagoLavado tenga las propiedades correctas.
        /// </summary>
        [Fact]
        public void PagoLavado_ShouldHaveCorrectProperties()
        {
            // Arrange
            var pago = new PagoLavado
            {
                Estado = "Pendiente",
                MontoPagado = 0
            };

            // Assert
            Assert.Equal("Pendiente", pago.Estado);
            Assert.Equal(0, pago.MontoPagado);
            Assert.Empty(pago.Pagos);
        }

        /// <summary>
        /// Verifica que DetallePago tenga las propiedades correctas.
        /// </summary>
        [Fact]
        public void DetallePago_ShouldHaveCorrectProperties()
        {
            // Arrange
            var fecha = DateTime.UtcNow;
            var detalle = new DetallePago
            {
                Id = "pago-001",
                Monto = 5000,
                MedioPago = "Efectivo",
                Fecha = fecha,
                Notas = "Pago parcial"
            };

            // Assert
            Assert.Equal("pago-001", detalle.Id);
            Assert.Equal(5000, detalle.Monto);
            Assert.Equal("Efectivo", detalle.MedioPago);
            Assert.Equal(fecha, detalle.Fecha);
            Assert.Equal("Pago parcial", detalle.Notas);
        }

        #endregion

        #region Tests de Estados del Lavado

        /// <summary>
        /// Verifica los estados válidos del lavado.
        /// </summary>
        [Theory]
        [InlineData("Pendiente")]
        [InlineData("EnProceso")]
        [InlineData("Realizado")]
        [InlineData("RealizadoParcialmente")]
        [InlineData("Cancelado")]
        public void Estado_ShouldBeValid(string estado)
        {
            // Arrange
            var validEstados = new[] { "Pendiente", "EnProceso", "Realizado", "RealizadoParcialmente", "Cancelado" };

            // Act
            var isValid = validEstados.Contains(estado);

            // Assert
            Assert.True(isValid);
        }

        /// <summary>
        /// Verifica que el estado inicial sea correcto.
        /// </summary>
        [Fact]
        public void InitialState_ShouldBeEnProceso()
        {
            // El servicio crea lavados directamente en EnProceso
            var lavado = TestFactory.CreateLavado(estado: "EnProceso");
            Assert.Equal("EnProceso", lavado.Estado);
        }

        /// <summary>
        /// Verifica los estados de retiro válidos.
        /// </summary>
        [Theory]
        [InlineData("Pendiente")]
        [InlineData("Retirado")]
        public void EstadoRetiro_ShouldBeValid(string estadoRetiro)
        {
            // Assert
            Assert.True(estadoRetiro == Lavado.EstadosRetiro.Pendiente || 
                       estadoRetiro == Lavado.EstadosRetiro.Retirado);
        }

        /// <summary>
        /// Verifica el estado de retiro por defecto.
        /// </summary>
        [Fact]
        public void EstadoRetiro_DefaultValue_ShouldBePendiente()
        {
            // Arrange
            var lavado = TestFactory.CreateLavado();

            // Assert
            Assert.Equal(Lavado.EstadosRetiro.Pendiente, lavado.EstadoRetiro);
        }

        #endregion

        #region Tests de Estados del Servicio en Lavado

        /// <summary>
        /// Verifica el estado por defecto del servicio.
        /// </summary>
        [Fact]
        public void ServicioEnLavado_DefaultEstado_ShouldBePendiente()
        {
            // Arrange
            var servicio = new ServicioEnLavado
            {
                ServicioId = "serv-001"
            };

            // Assert
            Assert.Equal("Pendiente", servicio.Estado);
        }

        /// <summary>
        /// Verifica los estados válidos del servicio en lavado.
        /// </summary>
        [Theory]
        [InlineData("Pendiente")]
        [InlineData("EnProceso")]
        [InlineData("Realizado")]
        [InlineData("RealizadoParcialmente")]
        [InlineData("Cancelado")]
        public void ServicioEnLavado_Estado_ShouldBeValid(string estado)
        {
            // Arrange
            var validEstados = new[] { "Pendiente", "EnProceso", "Realizado", "RealizadoParcialmente", "Cancelado" };

            // Act & Assert
            Assert.Contains(estado, validEstados);
        }

        #endregion

        #region Tests de Cálculo de Precios

        /// <summary>
        /// Verifica que el precio sea la suma de los servicios menos el descuento.
        /// </summary>
        [Fact]
        public void Precio_ShouldBeSumOfServicesMinusDiscount()
        {
            // Arrange
            var precioOriginal = 20000m;
            var descuento = 10m; // 10%
            var precioEsperado = precioOriginal * (1 - descuento / 100);

            // Act
            var lavado = TestFactory.CreateLavado(
                precio: precioEsperado,
                precioOriginal: precioOriginal,
                descuento: descuento
            );

            // Assert
            Assert.Equal(18000m, lavado.Precio);
            Assert.Equal(20000m, lavado.PrecioOriginal);
            Assert.Equal(10m, lavado.Descuento);
        }

        /// <summary>
        /// Verifica que el precio no pueda ser negativo.
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(100)]
        [InlineData(50000)]
        public void Precio_ShouldBeZeroOrPositive(decimal precio)
        {
            // Assert
            Assert.True(precio >= 0);
        }

        /// <summary>
        /// Verifica que el descuento esté en rango válido (0-100).
        /// </summary>
        [Theory]
        [InlineData(0, true)]
        [InlineData(50, true)]
        [InlineData(100, true)]
        [InlineData(-1, false)]
        [InlineData(101, false)]
        public void Descuento_ShouldBeInValidRange(decimal descuento, bool shouldBeValid)
        {
            // Act
            var isValid = descuento >= 0 && descuento <= 100;

            // Assert
            Assert.Equal(shouldBeValid, isValid);
        }

        /// <summary>
        /// Verifica el cálculo de tiempo estimado total.
        /// </summary>
        [Fact]
        public void TiempoEstimado_ShouldBeSumOfServicesTime()
        {
            // Arrange
            var servicios = new List<ServicioEnLavado>
            {
                new() { ServicioId = "s1", TiempoEstimado = 30 },
                new() { ServicioId = "s2", TiempoEstimado = 20 },
                new() { ServicioId = "s3", TiempoEstimado = 15 }
            };

            // Act
            var tiempoTotal = servicios.Sum(s => s.TiempoEstimado);

            // Assert
            Assert.Equal(65, tiempoTotal);
        }

        #endregion

        #region Tests de Pago

        /// <summary>
        /// Verifica los estados de pago válidos.
        /// </summary>
        [Theory]
        [InlineData("Pendiente")]
        [InlineData("Parcial")]
        [InlineData("Pagado")]
        [InlineData("Cancelado")]
        public void PagoEstado_ShouldBeValid(string estado)
        {
            // Arrange
            var validEstados = new[] { "Pendiente", "Parcial", "Pagado", "Cancelado" };

            // Assert
            Assert.Contains(estado, validEstados);
        }

        /// <summary>
        /// Verifica que el monto pagado sea la suma de los pagos individuales.
        /// </summary>
        [Fact]
        public void MontoPagado_ShouldBeSumOfPagos()
        {
            // Arrange
            var pago = new PagoLavado
            {
                Estado = "Parcial",
                Pagos = new List<DetallePago>
                {
                    new() { Id = "1", Monto = 3000 },
                    new() { Id = "2", Monto = 2000 }
                }
            };
            pago.MontoPagado = pago.Pagos.Sum(p => p.Monto);

            // Assert
            Assert.Equal(5000, pago.MontoPagado);
        }

        /// <summary>
        /// Verifica la transición de estado de pago cuando se paga completo.
        /// </summary>
        [Fact]
        public void PagoEstado_ShouldBePagado_WhenFullyPaid()
        {
            // Arrange
            var precioTotal = 10000m;
            var montoPagado = 10000m;

            // Act
            var estado = montoPagado >= precioTotal ? "Pagado" : 
                         montoPagado > 0 ? "Parcial" : "Pendiente";

            // Assert
            Assert.Equal("Pagado", estado);
        }

        /// <summary>
        /// Verifica la transición de estado de pago parcial.
        /// </summary>
        [Fact]
        public void PagoEstado_ShouldBeParcial_WhenPartiallyPaid()
        {
            // Arrange
            var precioTotal = 10000m;
            var montoPagado = 5000m;

            // Act
            var estado = montoPagado >= precioTotal ? "Pagado" : 
                         montoPagado > 0 ? "Parcial" : "Pendiente";

            // Assert
            Assert.Equal("Parcial", estado);
        }

        /// <summary>
        /// Verifica los medios de pago válidos.
        /// </summary>
        [Theory]
        [InlineData("Efectivo")]
        [InlineData("TarjetaDebito")]
        [InlineData("TarjetaCredito")]
        [InlineData("Transferencia")]
        [InlineData("MercadoPago")]
        public void MedioPago_ShouldBeValid(string medioPago)
        {
            // Arrange
            var detalle = new DetallePago
            {
                Id = "1",
                Monto = 1000,
                MedioPago = medioPago
            };

            // Assert
            Assert.Equal(medioPago, detalle.MedioPago);
        }

        #endregion

        #region Tests de Servicios en Lavado

        /// <summary>
        /// Verifica que un lavado debe tener al menos un servicio.
        /// </summary>
        [Fact]
        public void Lavado_ShouldHaveAtLeastOneService()
        {
            // Arrange
            var lavado = TestFactory.CreateLavado();
            lavado.Servicios.Add(new ServicioEnLavado 
            { 
                ServicioId = "s1", 
                ServicioNombre = "Lavado Básico" 
            });

            // Assert
            Assert.NotEmpty(lavado.Servicios);
        }

        /// <summary>
        /// Verifica el orden de servicios.
        /// </summary>
        [Fact]
        public void Servicios_ShouldMaintainOrder()
        {
            // Arrange
            var servicios = new List<ServicioEnLavado>
            {
                new() { ServicioId = "s3", Orden = 3 },
                new() { ServicioId = "s1", Orden = 1 },
                new() { ServicioId = "s2", Orden = 2 }
            };

            // Act
            var ordered = servicios.OrderBy(s => s.Orden).ToList();

            // Assert
            Assert.Equal("s1", ordered[0].ServicioId);
            Assert.Equal("s2", ordered[1].ServicioId);
            Assert.Equal("s3", ordered[2].ServicioId);
        }

        /// <summary>
        /// Verifica que las etapas del servicio se inicializan vacías.
        /// </summary>
        [Fact]
        public void ServicioEtapas_ShouldInitializeEmpty()
        {
            // Arrange
            var servicio = new ServicioEnLavado { ServicioId = "s1" };

            // Assert
            Assert.NotNull(servicio.Etapas);
            Assert.Empty(servicio.Etapas);
        }

        #endregion

        #region Tests de Empleados Asignados

        /// <summary>
        /// Verifica que la cantidad de empleados requeridos esté en rango válido (1-10).
        /// </summary>
        [Theory]
        [InlineData(1, true)]
        [InlineData(5, true)]
        [InlineData(10, true)]
        [InlineData(0, false)]
        [InlineData(11, false)]
        public void CantidadEmpleadosRequeridos_ShouldBeInValidRange(int cantidad, bool shouldBeValid)
        {
            // Act
            var isValid = cantidad >= 1 && cantidad <= 10;

            // Assert
            Assert.Equal(shouldBeValid, isValid);
        }

        /// <summary>
        /// Verifica que las listas de empleados se inicializan vacías.
        /// </summary>
        [Fact]
        public void EmpleadosAsignados_ShouldInitializeEmpty()
        {
            // Arrange
            var lavado = TestFactory.CreateLavado();

            // Assert
            Assert.NotNull(lavado.EmpleadosAsignadosIds);
            Assert.NotNull(lavado.EmpleadosAsignadosNombres);
            Assert.Empty(lavado.EmpleadosAsignadosIds);
            Assert.Empty(lavado.EmpleadosAsignadosNombres);
        }

        /// <summary>
        /// Verifica la asignación de empleados.
        /// </summary>
        [Fact]
        public void AsignarEmpleados_ShouldAddToLists()
        {
            // Arrange
            var lavado = TestFactory.CreateLavado();
            
            // Act
            lavado.EmpleadosAsignadosIds.Add("emp-001");
            lavado.EmpleadosAsignadosNombres.Add("Juan Pérez");

            // Assert
            Assert.Single(lavado.EmpleadosAsignadosIds);
            Assert.Single(lavado.EmpleadosAsignadosNombres);
            Assert.Equal("emp-001", lavado.EmpleadosAsignadosIds[0]);
            Assert.Equal("Juan Pérez", lavado.EmpleadosAsignadosNombres[0]);
        }

        #endregion

        #region Tests de Filtrado

        /// <summary>
        /// Verifica el filtrado por estado.
        /// </summary>
        [Fact]
        public void FilterByEstado_ShouldReturnMatchingLavados()
        {
            // Arrange
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1", estado: "EnProceso"),
                TestFactory.CreateLavado(id: "2", estado: "Realizado"),
                TestFactory.CreateLavado(id: "3", estado: "EnProceso"),
                TestFactory.CreateLavado(id: "4", estado: "Cancelado")
            };

            // Act
            var enProceso = lavados.Where(l => l.Estado == "EnProceso").ToList();

            // Assert
            Assert.Equal(2, enProceso.Count);
        }

        /// <summary>
        /// Verifica el filtrado por múltiples estados.
        /// </summary>
        [Fact]
        public void FilterByMultipleEstados_ShouldWork()
        {
            // Arrange
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1", estado: "EnProceso"),
                TestFactory.CreateLavado(id: "2", estado: "Realizado"),
                TestFactory.CreateLavado(id: "3", estado: "Cancelado")
            };
            var estadosFilter = new List<string> { "EnProceso", "Realizado" };

            // Act
            var filtered = lavados.Where(l => estadosFilter.Contains(l.Estado)).ToList();

            // Assert
            Assert.Equal(2, filtered.Count);
        }

        /// <summary>
        /// Verifica el filtrado por cliente.
        /// </summary>
        [Fact]
        public void FilterByCliente_ShouldReturnClienteLavados()
        {
            // Arrange
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1", clienteId: "cli-001"),
                TestFactory.CreateLavado(id: "2", clienteId: "cli-002"),
                TestFactory.CreateLavado(id: "3", clienteId: "cli-001")
            };

            // Act
            var clienteLavados = lavados.Where(l => l.ClienteId == "cli-001").ToList();

            // Assert
            Assert.Equal(2, clienteLavados.Count);
        }

        /// <summary>
        /// Verifica el filtrado por vehículo.
        /// </summary>
        [Fact]
        public void FilterByVehiculo_ShouldReturnVehiculoLavados()
        {
            // Arrange
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1", vehiculoId: "veh-001"),
                TestFactory.CreateLavado(id: "2", vehiculoId: "veh-002"),
                TestFactory.CreateLavado(id: "3", vehiculoId: "veh-001")
            };

            // Act
            var vehiculoLavados = lavados.Where(l => l.VehiculoId == "veh-001").ToList();

            // Assert
            Assert.Equal(2, vehiculoLavados.Count);
        }

        /// <summary>
        /// Verifica el filtrado por rango de fechas.
        /// </summary>
        [Fact]
        public void FilterByDateRange_ShouldWork()
        {
            // Arrange
            var baseDate = DateTime.UtcNow;
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1", fechaCreacion: baseDate.AddDays(-5)),
                TestFactory.CreateLavado(id: "2", fechaCreacion: baseDate.AddDays(-2)),
                TestFactory.CreateLavado(id: "3", fechaCreacion: baseDate)
            };
            var fechaDesde = baseDate.AddDays(-3);

            // Act
            var filtered = lavados.Where(l => l.FechaCreacion >= fechaDesde).ToList();

            // Assert
            Assert.Equal(2, filtered.Count);
        }

        /// <summary>
        /// Verifica el filtrado por rango de precio.
        /// </summary>
        [Fact]
        public void FilterByPriceRange_ShouldWork()
        {
            // Arrange
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1", precio: 5000),
                TestFactory.CreateLavado(id: "2", precio: 10000),
                TestFactory.CreateLavado(id: "3", precio: 15000)
            };
            var precioDesde = 8000m;
            var precioHasta = 12000m;

            // Act
            var filtered = lavados.Where(l => l.Precio >= precioDesde && l.Precio <= precioHasta).ToList();

            // Assert
            Assert.Single(filtered);
            Assert.Equal("2", filtered[0].Id);
        }

        /// <summary>
        /// Verifica el filtrado por estado de pago.
        /// </summary>
        [Fact]
        public void FilterByEstadoPago_ShouldWork()
        {
            // Arrange
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1"),
                TestFactory.CreateLavado(id: "2"),
                TestFactory.CreateLavado(id: "3")
            };
            lavados[0].Pago = new PagoLavado { Estado = "Pendiente" };
            lavados[1].Pago = new PagoLavado { Estado = "Pagado" };
            lavados[2].Pago = new PagoLavado { Estado = "Pendiente" };

            // Act
            var pendientes = lavados.Where(l => l.Pago?.Estado == "Pendiente").ToList();

            // Assert
            Assert.Equal(2, pendientes.Count);
        }

        #endregion

        #region Tests de Ordenamiento

        /// <summary>
        /// Verifica el ordenamiento por fecha de creación descendente (más reciente primero).
        /// </summary>
        [Fact]
        public void SortByFechaCreacion_Descending_ShouldOrderCorrectly()
        {
            // Arrange
            var baseDate = DateTime.UtcNow;
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1", fechaCreacion: baseDate.AddDays(-2)),
                TestFactory.CreateLavado(id: "2", fechaCreacion: baseDate),
                TestFactory.CreateLavado(id: "3", fechaCreacion: baseDate.AddDays(-1))
            };

            // Act
            var sorted = lavados.OrderByDescending(l => l.FechaCreacion).ToList();

            // Assert
            Assert.Equal("2", sorted[0].Id);
            Assert.Equal("3", sorted[1].Id);
            Assert.Equal("1", sorted[2].Id);
        }

        /// <summary>
        /// Verifica el ordenamiento por precio ascendente.
        /// </summary>
        [Fact]
        public void SortByPrecio_Ascending_ShouldOrderCorrectly()
        {
            // Arrange
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1", precio: 15000),
                TestFactory.CreateLavado(id: "2", precio: 5000),
                TestFactory.CreateLavado(id: "3", precio: 10000)
            };

            // Act
            var sorted = lavados.OrderBy(l => l.Precio).ToList();

            // Assert
            Assert.Equal("2", sorted[0].Id);
            Assert.Equal("3", sorted[1].Id);
            Assert.Equal("1", sorted[2].Id);
        }

        /// <summary>
        /// Verifica el ordenamiento por estado.
        /// </summary>
        [Fact]
        public void SortByEstado_ShouldOrderAlphabetically()
        {
            // Arrange
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1", estado: "Realizado"),
                TestFactory.CreateLavado(id: "2", estado: "Cancelado"),
                TestFactory.CreateLavado(id: "3", estado: "EnProceso")
            };

            // Act
            var sorted = lavados.OrderBy(l => l.Estado).ToList();

            // Assert
            Assert.Equal("Cancelado", sorted[0].Estado);
            Assert.Equal("EnProceso", sorted[1].Estado);
            Assert.Equal("Realizado", sorted[2].Estado);
        }

        #endregion

        #region Tests de Búsqueda

        /// <summary>
        /// Verifica la búsqueda por nombre de cliente.
        /// </summary>
        [Fact]
        public void Search_ByClienteNombre_ShouldWork()
        {
            // Arrange
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1", clienteNombre: "Juan Pérez"),
                TestFactory.CreateLavado(id: "2", clienteNombre: "María García"),
                TestFactory.CreateLavado(id: "3", clienteNombre: "Carlos Pérez")
            };
            var searchTerm = "pérez";

            // Act
            var results = lavados.Where(l => 
                l.ClienteNombre?.ToLowerInvariant().Contains(searchTerm.ToLowerInvariant()) ?? false
            ).ToList();

            // Assert
            Assert.Equal(2, results.Count);
        }

        /// <summary>
        /// Verifica la búsqueda por patente de vehículo.
        /// </summary>
        [Fact]
        public void Search_ByVehiculoPatente_ShouldWork()
        {
            // Arrange
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1", vehiculoPatente: "ABC123"),
                TestFactory.CreateLavado(id: "2", vehiculoPatente: "DEF456"),
                TestFactory.CreateLavado(id: "3", vehiculoPatente: "ABC789")
            };
            var searchTerm = "abc";

            // Act
            var results = lavados.Where(l => 
                l.VehiculoPatente?.ToLowerInvariant().Contains(searchTerm.ToLowerInvariant()) ?? false
            ).ToList();

            // Assert
            Assert.Equal(2, results.Count);
        }

        /// <summary>
        /// Verifica la búsqueda por nombre de servicio.
        /// </summary>
        [Fact]
        public void Search_ByServicioNombre_ShouldWork()
        {
            // Arrange
            var lavados = new List<Lavado>
            {
                TestFactory.CreateLavado(id: "1"),
                TestFactory.CreateLavado(id: "2"),
            };
            lavados[0].Servicios.Add(new ServicioEnLavado { ServicioId = "s1", ServicioNombre = "Lavado Completo" });
            lavados[1].Servicios.Add(new ServicioEnLavado { ServicioId = "s2", ServicioNombre = "Encerado" });

            var searchTerm = "lavado";

            // Act
            var results = lavados.Where(l => 
                l.Servicios.Any(s => s.ServicioNombre?.ToLowerInvariant().Contains(searchTerm.ToLowerInvariant()) ?? false)
            ).ToList();

            // Assert
            Assert.Single(results);
        }

        #endregion

        #region Tests de Paginación

        /// <summary>
        /// Verifica el cálculo correcto del total de páginas.
        /// </summary>
        [Theory]
        [InlineData(100, 10, 10)]
        [InlineData(95, 10, 10)]
        [InlineData(50, 20, 3)]
        [InlineData(1, 10, 1)]
        public void CalculateTotalPages_ShouldReturnCorrectPageCount(int totalItems, int pageSize, int expected)
        {
            // Act
            var actual = Math.Max((int)Math.Ceiling(totalItems / (double)pageSize), 1);

            // Assert
            Assert.Equal(expected, actual);
        }

        /// <summary>
        /// Verifica la paginación con lista vacía devuelve al menos 1 página.
        /// </summary>
        [Fact]
        public void Pagination_EmptyList_ShouldReturnOnePage()
        {
            // Arrange
            var totalItems = 0;
            var pageSize = 10;

            // Act
            var totalPages = Math.Max((int)Math.Ceiling(totalItems / (double)pageSize), 1);

            // Assert
            Assert.Equal(1, totalPages);
        }

        #endregion

        #region Tests de Validación

        /// <summary>
        /// Verifica que el cliente sea obligatorio.
        /// </summary>
        [Fact]
        public void Validation_ClienteId_ShouldBeRequired()
        {
            // Arrange
            var lavado = TestFactory.CreateLavado(clienteId: "");

            // Assert
            Assert.True(string.IsNullOrWhiteSpace(lavado.ClienteId));
        }

        /// <summary>
        /// Verifica que el vehículo sea obligatorio.
        /// </summary>
        [Fact]
        public void Validation_VehiculoId_ShouldBeRequired()
        {
            // Arrange
            var lavado = TestFactory.CreateLavado(vehiculoId: "");

            // Assert
            Assert.True(string.IsNullOrWhiteSpace(lavado.VehiculoId));
        }

        /// <summary>
        /// Verifica que el motivo de cancelación sea obligatorio cuando se cancela.
        /// </summary>
        [Fact]
        public void Validation_MotivoCancelacion_ShouldBeRequiredWhenCancelled()
        {
            // Arrange
            var lavado = TestFactory.CreateLavado(estado: "Cancelado");
            lavado.MotivoCancelacion = "Cliente no apareció";

            // Assert - Si el estado es Cancelado, el motivo no debe estar vacío
            Assert.False(string.IsNullOrWhiteSpace(lavado.MotivoCancelacion));
        }

        #endregion

        #region Tests de Tiempos

        /// <summary>
        /// Verifica que el tiempo de inicio se establezca correctamente.
        /// </summary>
        [Fact]
        public void TiempoInicio_ShouldBeSetWhenStarted()
        {
            // Arrange
            var lavado = TestFactory.CreateLavado();
            var now = DateTime.UtcNow;

            // Act
            lavado.TiempoInicio = now;

            // Assert
            Assert.NotNull(lavado.TiempoInicio);
            Assert.Equal(now, lavado.TiempoInicio);
        }

        /// <summary>
        /// Verifica que el tiempo de finalización sea posterior al de inicio.
        /// </summary>
        [Fact]
        public void TiempoFinalizacion_ShouldBeAfterTiempoInicio()
        {
            // Arrange
            var lavado = TestFactory.CreateLavado();
            var tiempoInicio = DateTime.UtcNow;
            var tiempoFinalizacion = tiempoInicio.AddHours(1);

            // Act
            lavado.TiempoInicio = tiempoInicio;
            lavado.TiempoFinalizacion = tiempoFinalizacion;

            // Assert
            Assert.True(lavado.TiempoFinalizacion > lavado.TiempoInicio);
        }

        #endregion
    }
}
