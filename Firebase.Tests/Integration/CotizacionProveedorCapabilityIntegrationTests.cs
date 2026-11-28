using Firebase.Models;
using Firebase.Services;
using Google.Api.Gax;
using Google.Cloud.Firestore;

namespace Firebase.Tests.Integration;

public sealed class CotizacionProveedorCapabilityIntegrationTests
{
    private static async Task<(FirestoreDb Db, SolicitudCotizacion Solicitud)> PrepararSolicitud()
    {
        var db = new FirestoreDbBuilder
        {
            ProjectId = "demo-prov-link-" + Guid.NewGuid().ToString("N")[..16],
            EmulatorDetection = EmulatorDetection.EmulatorOnly
        }.Build();

        await db.Collection("proveedores").Document("p1").SetAsync(new Proveedor
        {
            Id = "p1",
            RazonSocial = "Química Sur",
            EmailOrdenes = "ventas@quimicasur.invalid",
            Estado = "Activo"
        });
        await db.Collection("insumos").Document("i1").SetAsync(new Insumo
        {
            Id = "i1",
            Nombre = "Cera pulidora",
            UnidadMedida = "g",
            Estado = "Activo"
        });
        await db.Collection("configuracion").Document("system_config").SetAsync(new Configuracion
        {
            NombreLavadero = "Lavadero Central"
        });

        var compras = new ComprasService(db);
        var creacion = await compras.CrearSolicitudesConDetalle(
            new() { "p1" },
            new() { new OrdenCompraItem { InsumoId = "i1", Cantidad = 1000m } },
            "Entregar por portón lateral");
        Assert.True(creacion.Resultado.EsExitoso);
        var solicitud = Assert.Single(creacion.Solicitudes!);
        return (db, solicitud);
    }

    [LocalFirestoreFact]
    public async Task Emitir_GuardaSoloHashYPermiteRotacionAlReenviar()
    {
        var (db, solicitud) = await PrepararSolicitud();
        var cap = new CotizacionProveedorCapability(db);

        var token1 = await cap.EmitirAsync(solicitud.Id);
        Assert.NotNull(token1);

        var capSnap1 = await db.Collection("cotizaciones_proveedor_capabilities").Document(solicitud.Id).GetSnapshotAsync();
        Assert.True(capSnap1.Exists);
        Assert.Equal(CotizacionProveedorCapability.Hash(token1!), capSnap1.GetValue<string>("Hash"));
        Assert.DoesNotContain(token1!, capSnap1.ToDictionary().Values.Select(v => v?.ToString() ?? ""));

        var token2 = await cap.EmitirAsync(solicitud.Id);
        Assert.NotNull(token2);
        Assert.NotEqual(token1, token2);

        await Assert.ThrowsAsync<CotizacionProveedorInvalidaException>(() => cap.ObtenerDetalleAsync(solicitud.Id, token1!));
        var detalle = await cap.ObtenerDetalleAsync(solicitud.Id, token2!);
        Assert.Equal("Lavadero Central", detalle.NombreLavadero);
        Assert.Equal("Entregar por portón lateral", detalle.NotasSolicitud);
        var item = Assert.Single(detalle.Items);
        Assert.Equal("Cera pulidora", item.NombreInsumo);
        Assert.Equal(1000m, item.Cantidad);
        Assert.Equal("g", item.UnidadMedida);
    }

    [LocalFirestoreFact]
    public async Task ResponderAsync_ConPrecioFinal_PersisteConsumeYBloqueaReintentoOManual()
    {
        var (db, solicitud) = await PrepararSolicitud();
        var cap = new CotizacionProveedorCapability(db);
        var token = await cap.EmitirAsync(solicitud.Id);
        Assert.NotNull(token);

        await cap.ResponderAsync(solicitud.Id, token!, new RespuestaCotizacionProveedorRequest
        {
            SolicitudId = solicitud.Id,
            Token = token,
            PlazoEntrega = "4",
            Notas = "Oferta válida 7 días",
            Items = new()
            {
                new RespuestaCotizacionProveedorItem
                {
                    InsumoId = "i1",
                    Precio = 15000m,
                    TipoPrecio = "Final",
                    Disponible = true
                }
            }
        });

        var solSnap = await db.Collection("solicitudesCotizacion").Document(solicitud.Id).GetSnapshotAsync();
        Assert.Equal("Respondida", solSnap.GetValue<string>("Estado"));

        var capSnap = await db.Collection("cotizaciones_proveedor_capabilities").Document(solicitud.Id).GetSnapshotAsync();
        Assert.True(capSnap.GetValue<bool>("Consumed"));

        var respDocs = (await db.Collection("cotizacionesRespuestas").WhereEqualTo("SolicitudId", solicitud.Id).GetSnapshotAsync()).Documents;
        var resp = Assert.Single(respDocs).ConvertTo<CotizacionRespuesta>();
        Assert.Equal(15m, Assert.Single(resp.Items).PrecioUnitario);
        Assert.Equal("4", resp.PlazoEntrega);

        await Assert.ThrowsAsync<CotizacionProveedorInvalidaException>(() => cap.ObtenerDetalleAsync(solicitud.Id, token!));
        await Assert.ThrowsAsync<CotizacionProveedorInvalidaException>(() => cap.ResponderAsync(solicitud.Id, token!, new RespuestaCotizacionProveedorRequest
        {
            SolicitudId = solicitud.Id,
            Token = token,
            PlazoEntrega = "4",
            Items = new() { new RespuestaCotizacionProveedorItem { InsumoId = "i1", Precio = 100m, TipoPrecio = "Unitario" } }
        }));

        var manual = await new ComprasService(db).RegistrarRespuesta(new CotizacionRespuesta
        {
            SolicitudId = solicitud.Id,
            PlazoEntrega = "2",
            Items = new() { new OrdenCompraItem { InsumoId = "i1", PrecioUnitario = 10m } }
        });
        Assert.False(manual.EsExitoso);
    }

    [LocalFirestoreFact]
    public async Task RespuestaManualPrevia_BloqueaElEnlaceDelProveedor()
    {
        var (db, solicitud) = await PrepararSolicitud();
        var cap = new CotizacionProveedorCapability(db);
        var token = await cap.EmitirAsync(solicitud.Id);
        Assert.NotNull(token);

        var manual = await new ComprasService(db).RegistrarRespuesta(new CotizacionRespuesta
        {
            SolicitudId = solicitud.Id,
            PlazoEntrega = "2",
            Items = new() { new OrdenCompraItem { InsumoId = "i1", PrecioUnitario = 20m } }
        });
        Assert.True(manual.EsExitoso);

        await Assert.ThrowsAsync<CotizacionProveedorInvalidaException>(() => cap.ObtenerDetalleAsync(solicitud.Id, token!));
        await Assert.ThrowsAsync<CotizacionProveedorInvalidaException>(() => cap.ResponderAsync(solicitud.Id, token!, new RespuestaCotizacionProveedorRequest
        {
            SolicitudId = solicitud.Id,
            Token = token,
            PlazoEntrega = "2",
            Items = new() { new RespuestaCotizacionProveedorItem { InsumoId = "i1", Precio = 10m } }
        }));
    }
}
