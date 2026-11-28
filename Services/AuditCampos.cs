using Firebase.Models;

/// <summary>
/// Definición de listas blancas de campos auditables por tipo de entidad.
/// Garantiza que nunca se serialicen credenciales, tokens, claves de asociación ni secretos.
/// Solo los campos explícitamente listados aquí son capturados en Antes/Después.
/// </summary>
public static class AuditCampos
{
    // ── Listas blancas ──────────────────────────────────────────────────────

    /// <summary>Campos auditables de Servicio.</summary>
    public static readonly IReadOnlyList<string> Servicio = new[]
    {
        "Nombre", "Precio", "Tipo", "TipoVehiculo", "TiempoEstimado", "Descripcion", "Estado", "Etapas"
    };

    /// <summary>Campos auditables de Cliente, incluidas referencias a vehículos.</summary>
    public static readonly IReadOnlyList<string> Cliente = new[]
    {
        "Nombre", "Apellido", "TipoDocumento", "NumeroDocumento", "Telefono", "Email", "Estado", "VehiculosIds"
    };

    /// <summary>Campos auditables de Vehiculo, sin clave de asociación.</summary>
    public static readonly IReadOnlyList<string> Vehiculo = new[]
    {
        "Patente", "TipoVehiculo", "Marca", "Modelo", "Color", "Estado", "ClienteId", "ClientesIds"
    };

    /// <summary>Campos auditables de Empleado. Excluye credenciales y tokens de autenticación.</summary>
    public static readonly IReadOnlyList<string> Empleado = new[]
    {
        "NombreCompleto", "Email", "Rol", "Estado"
    };

    /// <summary>Campos auditables de PaqueteServicio.</summary>
    public static readonly IReadOnlyList<string> Paquete = new[]
    {
        "Nombre", "TipoVehiculo", "PorcentajeDescuento", "Estado", "ServiciosIds", "Precio", "TiempoEstimado"
    };

    /// <summary>Campos auditables de Rol.</summary>
    public static readonly IReadOnlyList<string> Rol = new[]
    {
        "Nombre", "Descripcion", "Permisos", "Estado", "FechaCreacion"
    };

    /// <summary>Campos auditables de Configuracion (excluye datos operativos internos).</summary>
    public static readonly IReadOnlyList<string> Configuracion = new[]
    {
        "CancelacionAnticipadaDescuento", "CancelacionAnticipadaHorasMinimas",
        "CancelacionAnticipadaValidezDias", "PaquetesDescuentoStep",
        "CapacidadMaximaConcurrente", "SesionDuracionHoras", "SesionInactividadMinutos",
        "NombreLavadero", "Ubicacion", "HorariosOperacion", "ConsiderarEmpleadosActivos",
        "EmpleadosMaximosPorLavado", "TiempoNotificacionMinutos", "TiempoToleranciaMinutos", "IntervaloPreguntas",
        "PorcentajeAlertaStock", "VigenciaCompraRecienteDias", "IntervaloInicioTurnosMinutos", "RespuestaOfertaAdelantoMinutos", "ToleranciaLlegadaTurnosMinutos", "MargenTiempoInicioTurnoMinutos"
    };

    /// <summary>Campos auditables de Proveedor.</summary>
    public static readonly IReadOnlyList<string> Proveedor = new[]
    {
        "IdentificacionTributaria", "RazonSocial", "Contacto", "EmailOrdenes", "Estado"
    };

    /// <summary>Campos auditables de Insumo.</summary>
    public static readonly IReadOnlyList<string> Insumo = new[]
    {
        "Nombre", "Tipo", "UnidadMedida", "StockActual", "StockMinimo", "StockMaximo",
        "CantidadReabastecimiento", "ReabastecimientoAutomatico", "ProveedoresIds", "Estado"
    };

    /// <summary>Campos auditables de OrdenCompra.</summary>
    public static readonly IReadOnlyList<string> OrdenCompra = new[]
    {
        "ProveedorId", "FechaEmision", "Estado", "GrupoCotizacionId", "Items", "Notas", "MotivoCancelacion"
    };

    /// <summary>Campos auditables de SolicitudCotizacion.</summary>
    public static readonly IReadOnlyList<string> SolicitudCotizacion = new[]
    {
        "GrupoId", "ProveedorId", "Estado", "Items", "Notas", "FechaEntregaPreferidaUtc"
    };

    /// <summary>Campos auditables de TipoInsumo.</summary>
    public static readonly IReadOnlyList<string> TipoInsumo = new[]
    {
        "Nombre", "Categoria", "UnidadResumen", "EsProtegido", "Estado"
    };

    // ── Capturadores ────────────────────────────────────────────────────────

    /// <summary>Captura los campos auditables de un Servicio.</summary>
    public static Dictionary<string, string> CapturarServicio(Firebase.Models.Servicio s)
    {
        if (s == null) return new Dictionary<string, string>();
        return new Dictionary<string, string>
        {
            ["Nombre"] = s.Nombre ?? "",
            ["Precio"] = s.Precio.ToString(),
            ["Tipo"] = s.Tipo ?? "",
            ["TipoVehiculo"] = s.TipoVehiculo ?? "",
            ["TiempoEstimado"] = s.TiempoEstimado.ToString(),
            ["Descripcion"] = s.Descripcion ?? "",
            ["Estado"] = s.Estado ?? "",
            ["Etapas"] = System.Text.Json.JsonSerializer.Serialize(s.Etapas?.Select(e => new { e.Id, e.Nombre }))
        };
    }

    /// <summary>Captura los campos auditables de un Cliente.</summary>
    public static Dictionary<string, string> CapturarCliente(Firebase.Models.Cliente c)
    {
        if (c == null) return new Dictionary<string, string>();
        return new Dictionary<string, string>
        {
            ["Nombre"] = c.Nombre ?? "",
            ["Apellido"] = c.Apellido ?? "",
            ["TipoDocumento"] = c.TipoDocumento ?? "",
            ["NumeroDocumento"] = c.NumeroDocumento ?? "",
            ["Telefono"] = c.Telefono ?? "",
            ["Email"] = c.Email ?? "",
            ["NotificarPorWhatsApp"] = c.NotificarPorWhatsApp?.ToString() ?? "",
            ["NotificarPorEmail"] = c.NotificarPorEmail?.ToString() ?? "",
            ["Estado"] = c.Estado ?? "",
            ["VehiculosIds"] = System.Text.Json.JsonSerializer.Serialize(c.VehiculosIds)
        };
    }

    /// <summary>Captura los campos auditables de un Vehiculo sin su clave de asociación.</summary>
    public static Dictionary<string, string> CapturarVehiculo(Firebase.Models.Vehiculo v)
    {
        if (v == null) return new Dictionary<string, string>();
        return new Dictionary<string, string>
        {
            ["Patente"] = v.Patente ?? "",
            ["TipoVehiculo"] = v.TipoVehiculo ?? "",
            ["Marca"] = v.Marca ?? "",
            ["Modelo"] = v.Modelo ?? "",
            ["Color"] = v.Color ?? "",
            ["Estado"] = v.Estado ?? "",
            ["ClienteId"] = v.ClienteId ?? "",
            ["ClientesIds"] = System.Text.Json.JsonSerializer.Serialize(v.ClientesIds)
        };
    }

    /// <summary>Captura los campos auditables de un Empleado. Credenciales excluidas.</summary>
    public static Dictionary<string, string> CapturarEmpleado(Empleado e)
    {
        if (e == null) return new Dictionary<string, string>();
        return new Dictionary<string, string>
        {
            ["NombreCompleto"] = e.NombreCompleto ?? "",
            ["Email"] = e.Email ?? "",
            ["Rol"] = e.Rol ?? "",
            ["Estado"] = e.Estado ?? ""
        };
    }

    /// <summary>Captura los campos auditables de un PaqueteServicio.</summary>
    public static Dictionary<string, string> CapturarPaquete(Firebase.Models.PaqueteServicio p)
    {
        if (p == null) return new Dictionary<string, string>();
        return new Dictionary<string, string>
        {
            ["Nombre"] = p.Nombre ?? "",
            ["TipoVehiculo"] = p.TipoVehiculo ?? "",
            ["PorcentajeDescuento"] = p.PorcentajeDescuento.ToString(),
            ["Estado"] = p.Estado ?? "",
            ["ServiciosIds"] = System.Text.Json.JsonSerializer.Serialize(p.ServiciosIds ?? new()),
            ["Precio"] = p.Precio.ToString(),
            ["TiempoEstimado"] = p.TiempoEstimado.ToString()
        };
    }

    /// <summary>Captura los campos auditables de un Rol.</summary>
    public static Dictionary<string, string> CapturarRol(Firebase.Models.Rol r)
    {
        if (r == null) return new Dictionary<string, string>();
        return new Dictionary<string, string>
        {
            ["Nombre"] = r.Nombre ?? "",
            ["Descripcion"] = r.Descripcion ?? "",
            ["Permisos"] = System.Text.Json.JsonSerializer.Serialize((r.Permisos ?? new()).OrderBy(p => p, StringComparer.Ordinal)),
            ["Estado"] = r.Estado ?? "",
            ["FechaCreacion"] = r.FechaCreacion.ToUniversalTime().ToString("O")
        };
    }

    /// <summary>Captura los campos auditables de la Configuracion.</summary>
    public static Dictionary<string, string> CapturarConfiguracion(Firebase.Models.Configuracion c)
    {
        if (c == null) return new Dictionary<string, string>();
        return new Dictionary<string, string>
        {
            ["CancelacionAnticipadaDescuento"] = c.CancelacionAnticipadaDescuento.ToString(),
            ["CancelacionAnticipadaHorasMinimas"] = c.CancelacionAnticipadaHorasMinimas.ToString(),
            ["CancelacionAnticipadaValidezDias"] = c.CancelacionAnticipadaValidezDias.ToString(),
            ["PaquetesDescuentoStep"] = c.PaquetesDescuentoStep.ToString(),
            ["CapacidadMaximaConcurrente"] = c.CapacidadMaximaConcurrente.ToString(),
            ["SesionDuracionHoras"] = c.SesionDuracionHoras.ToString(),
            ["SesionInactividadMinutos"] = c.SesionInactividadMinutos.ToString(),
            ["NombreLavadero"] = c.NombreLavadero ?? "",
            ["Ubicacion"] = c.Ubicacion ?? "",
            ["HorariosOperacion"] = System.Text.Json.JsonSerializer.Serialize((c.HorariosOperacion ?? new()).OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value)),
            ["ConsiderarEmpleadosActivos"] = c.ConsiderarEmpleadosActivos.ToString(),
            ["EmpleadosMaximosPorLavado"] = c.EmpleadosMaximosPorLavado.ToString(),
            ["TiempoNotificacionMinutos"] = c.TiempoNotificacionMinutos.ToString(),
            ["TiempoToleranciaMinutos"] = c.TiempoToleranciaMinutos.ToString(),
            ["IntervaloPreguntas"] = c.IntervaloPreguntas.ToString(),
            ["PorcentajeAlertaStock"] = c.PorcentajeAlertaStock.ToString(),
            ["VigenciaCompraRecienteDias"] = c.VigenciaCompraRecienteDias.ToString(),
            ["IntervaloInicioTurnosMinutos"] = c.IntervaloInicioTurnosMinutos.ToString(),
            ["RespuestaOfertaAdelantoMinutos"] = c.RespuestaOfertaAdelantoMinutos.ToString(),
            ["ToleranciaLlegadaTurnosMinutos"] = c.ToleranciaLlegadaTurnosMinutos.ToString(),
            ["MargenTiempoInicioTurnoMinutos"] = c.MargenTiempoInicioTurnoMinutos.ToString()
        };
    }

    public static Dictionary<string, string> CapturarTipoServicio(TipoServicio? tipo) => tipo == null ? new() : new()
    {
        ["Nombre"] = tipo.Nombre ?? "",
        ["Estado"] = string.IsNullOrWhiteSpace(tipo.Estado) ? "Activo" : tipo.Estado
    };

    public static Dictionary<string, string> CapturarTipoDocumento(TipoDocumento tipo) => tipo == null ? new() : new()
    {
        ["Nombre"] = tipo.Nombre ?? "", ["Formato"] = tipo.Formato ?? "",
        ["Estado"] = string.IsNullOrWhiteSpace(tipo.Estado) ? "Activo" : tipo.Estado
    };

    public static Dictionary<string, string> CapturarTipoVehiculo(TipoVehiculo tipo) => tipo == null ? new() : new()
    {
        ["Nombre"] = tipo.Nombre ?? "", ["FormatoPatente"] = tipo.FormatoPatente ?? "",
        ["CantidadEmpleadosRequeridos"] = tipo.CantidadEmpleadosRequeridos.ToString(),
        ["Estado"] = string.IsNullOrWhiteSpace(tipo.Estado) ? "Activo" : tipo.Estado
    };

    /// <summary>Captura los campos auditables de un Proveedor.</summary>
    public static Dictionary<string, string> CapturarProveedor(Proveedor? p)
    {
        if (p == null) return new Dictionary<string, string>();
        return new Dictionary<string, string>
        {
            ["IdentificacionTributaria"] = p.IdentificacionTributaria ?? "",
            ["RazonSocial"] = p.RazonSocial ?? "",
            ["Contacto"] = p.Contacto ?? "",
            ["EmailOrdenes"] = p.EmailOrdenes ?? "",
            ["Estado"] = p.Estado ?? ""
        };
    }

    /// <summary>Captura los campos auditables de un Insumo.</summary>
    public static Dictionary<string, string> CapturarInsumo(Insumo? i)
    {
        if (i == null) return new Dictionary<string, string>();
        return new Dictionary<string, string>
        {
            ["Nombre"] = i.Nombre ?? "",
            ["Tipo"] = i.Tipo ?? "",
            ["UnidadMedida"] = i.UnidadMedida ?? "",
            ["StockActual"] = i.StockActual.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["StockMinimo"] = i.StockMinimo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["StockMaximo"] = i.StockMaximo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["CantidadReabastecimiento"] = i.CantidadReabastecimiento.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["ReabastecimientoAutomatico"] = i.ReabastecimientoAutomatico.ToString(),
            ["ProveedoresIds"] = System.Text.Json.JsonSerializer.Serialize((i.ProveedoresIds ?? new()).OrderBy(p => p, StringComparer.Ordinal)),
            ["Estado"] = i.Estado ?? ""
        };
    }

    /// <summary>Captura los campos auditables de un TipoInsumo.</summary>
    public static Dictionary<string, string> CapturarTipoInsumo(Firebase.Models.TipoInsumo? t)
    {
        if (t == null) return new Dictionary<string, string>();
        return new Dictionary<string, string>
        {
            ["Nombre"] = t.Nombre ?? "",
            ["Categoria"] = t.Categoria ?? "",
            ["UnidadResumen"] = t.UnidadResumen ?? "",
            ["EsProtegido"] = t.EsProtegido.ToString(),
            ["EsReutilizable"] = t.EsReutilizable.ToString(),
            ["Estado"] = t.Estado ?? ""
        };
    }

    /// <summary>Captura los campos auditables de una OrdenCompra.</summary>
    public static Dictionary<string, string> CapturarOrdenCompra(Firebase.Models.OrdenCompra? o)
    {
        if (o == null) return new Dictionary<string, string>();
        return new Dictionary<string, string>
        {
            ["ProveedorId"] = o.ProveedorId ?? "",
            ["FechaEmision"] = o.FechaEmision.ToUniversalTime().ToString("O"),
            ["Estado"] = o.Estado ?? "",
            ["GrupoCotizacionId"] = o.GrupoCotizacionId ?? "",
            ["Items"] = System.Text.Json.JsonSerializer.Serialize((o.Items ?? new()).Select(i => new
            {
                InsumoId = i.InsumoId ?? "",
                NombreInsumo = i.NombreInsumo ?? "",
                i.Cantidad,
                i.CantidadRecibida,
                i.PrecioUnitario,
                i.Disponible
            })),
            ["Notas"] = o.Notas ?? "",
            ["MotivoCancelacion"] = o.MotivoCancelacion ?? ""
        };
    }

    /// <summary>Captura los campos auditables de una SolicitudCotizacion.</summary>
    public static Dictionary<string, string> CapturarSolicitudCotizacion(Firebase.Models.SolicitudCotizacion? s)
    {
        if (s == null) return new Dictionary<string, string>();
        return new Dictionary<string, string>
        {
            ["GrupoId"] = s.GrupoId ?? "",
            ["ProveedorId"] = s.ProveedorId ?? "",
            ["Estado"] = s.Estado ?? "",
            ["Items"] = System.Text.Json.JsonSerializer.Serialize((s.Items ?? new()).Select(i => new
            {
                InsumoId = i.InsumoId ?? "",
                NombreInsumo = i.NombreInsumo ?? "",
                i.Cantidad,
                i.PrecioUnitario
            })),
            ["Notas"] = s.Notas ?? "",
            ["FechaEntregaPreferidaUtc"] = s.FechaEntregaPreferidaUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? "",
            ["EmailEnviado"] = s.EmailEnviado ? "true" : "false",
            ["FechaEnvioEmailUtc"] = s.FechaEnvioEmailUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture) ?? ""
        };
    }

    /// <summary>Captura explícita de los datos operativos del lavado, sin serializar entidades completas.</summary>
    public static Dictionary<string, string> CapturarLavado(Firebase.Models.Lavado l)
    {
        if (l == null) return new();
        return new()
        {
            ["Estado"] = l.Estado ?? "", ["ClienteId"] = l.ClienteId ?? "", ["ClienteNombre"] = l.ClienteNombre ?? "",
            ["VehiculoId"] = l.VehiculoId ?? "", ["VehiculoPatente"] = l.VehiculoPatente ?? "", ["TipoVehiculo"] = l.TipoVehiculo ?? "",
            ["Precio"] = l.Precio.ToString(System.Globalization.CultureInfo.InvariantCulture), ["PrecioOriginal"] = l.PrecioOriginal.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Descuento"] = l.Descuento.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["CantidadEmpleadosRequeridos"] = l.CantidadEmpleadosRequeridos.ToString(),
            ["EmpleadosAsignadosIds"] = System.Text.Json.JsonSerializer.Serialize(l.EmpleadosAsignadosIds),
            ["EmpleadosAsignadosNombres"] = System.Text.Json.JsonSerializer.Serialize(l.EmpleadosAsignadosNombres),
            ["TiempoEstimado"] = l.TiempoEstimado.ToString(), ["TiempoInicio"] = l.TiempoInicio?.ToString("O") ?? "",
            ["TiempoFinalizacion"] = l.TiempoFinalizacion?.ToString("O") ?? "", ["FechaCreacion"] = l.FechaCreacion.ToString("O"),
            ["MotivoCancelacion"] = l.MotivoCancelacion ?? "", ["Notas"] = l.Notas ?? "", ["EstadoRetiro"] = l.EstadoRetiro ?? "",
            ["ClienteTrajoId"] = l.ClienteTrajoId ?? "", ["ClienteTrajoNombre"] = l.ClienteTrajoNombre ?? "",
            ["ClienteRetiraId"] = l.ClienteRetiraId ?? "", ["ClienteRetiraNombre"] = l.ClienteRetiraNombre ?? "", ["FechaRetiro"] = l.FechaRetiro?.ToString("O") ?? "",
            ["Servicios"] = System.Text.Json.JsonSerializer.Serialize(l.Servicios?.Select(s => new { s.ServicioId, s.ServicioNombre, s.TipoServicio, s.Precio, s.TiempoEstimado, s.Estado, s.Orden, s.TiempoInicio, s.TiempoFinalizacion, s.MotivoCancelacion, s.PaqueteId, s.PaqueteNombre, Etapas = s.Etapas?.Select(e => new { e.EtapaId, e.Nombre, e.Estado, e.TiempoInicio, e.TiempoFinalizacion, e.MotivoCancelacion }) })),
            ["Pago"] = System.Text.Json.JsonSerializer.Serialize(l.Pago == null ? null : new { l.Pago.Estado, l.Pago.MontoPagado, Pagos = l.Pago.Pagos?.Select(p => new { p.Id, p.Monto, p.MedioPago, p.Fecha, p.Notas }) })
        };
    }

    // ── Estado histórico ────────────────────────────────────────────────────

    /// <summary>
    /// Compara dos capturas para determinar si hay cambios efectivos en los valores auditables.
    /// </summary>
    public static bool HayCambios(IReadOnlyDictionary<string, string>? antes, IReadOnlyDictionary<string, string>? despues)
    {
        if (antes == null && despues == null) return false;
        if (antes == null || despues == null) return true;
        return antes.Count != despues.Count ||
               antes.Any(kv => !despues.TryGetValue(kv.Key, out var valor) || !string.Equals(kv.Value, valor, StringComparison.Ordinal));
    }

    /// <summary>
    /// Determina el estado histórico de un registro de auditoría.
    /// <list type="bullet">
    ///   <item><description>null/null → "SinHistorial" (registro legado sin captura before/after)</description></item>
    ///   <item><description>vacío/vacío → "NoAplica" (evento de sesión, comparación no aplica)</description></item>
    ///   <item><description>cualquier otro caso → "ConCambios" (hay datos comparables)</description></item>
    /// </list>
    /// </summary>
    public static string ObtenerEstadoHistorial(
        Dictionary<string, string> antes,
        Dictionary<string, string> despues)
    {
        if (antes == null && despues == null)
            return "SinHistorial";

        if ((antes == null || antes.Count == 0) && (despues == null || despues.Count == 0))
            return "NoAplica";

        return "ConCambios";
    }
}
