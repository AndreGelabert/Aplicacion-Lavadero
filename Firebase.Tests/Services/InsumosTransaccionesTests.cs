using Firebase.Services;
using Google.Cloud.Firestore;
using Grpc.Core;
using Xunit;

namespace Firebase.Tests.Services;

public sealed class InsumosTransaccionesTests
{
    [Fact]
    public async Task EjecutarAsync_CuandoFallaConUnavailable_NoDebeReintentarCallbackParaEvitarDuplicacion()
    {
        var invocaciones = 0;
        var rpcException = new RpcException(new Status(StatusCode.Unavailable, "Socket closed during commit"));

        Func<FirestoreDb, Func<Transaction, Task<string>>, TransactionOptions, Task<string>> runnerFalso =
            (db, callback, options) =>
            {
                invocaciones++;
                throw rpcException;
            };

        var ex = await Assert.ThrowsAsync<RpcException>(() =>
            InsumosTransacciones.EjecutarAsync<string>(
                null!,
                _ => Task.FromResult("ok"),
                runnerFalso));

        Assert.Equal(StatusCode.Unavailable, ex.StatusCode);
        // Debe fallar de inmediato en el intento 1 sin reintentar,
        // porque el commit pudo haberse completado en el servidor y un reintento causaría duplicación.
        Assert.Equal(1, invocaciones);
    }

    [Fact]
    public async Task EjecutarAsync_CuandoFallaConAborted_DebeReintentarPorContencion()
    {
        var invocaciones = 0;
        var abortedException = new RpcException(new Status(StatusCode.Aborted, "Contention conflict"));

        Func<FirestoreDb, Func<Transaction, Task<string>>, TransactionOptions, Task<string>> runnerFalso =
            async (db, callback, options) =>
            {
                invocaciones++;
                if (invocaciones == 1)
                {
                    throw abortedException;
                }
                return await callback(null!);
            };

        var resultado = await InsumosTransacciones.EjecutarAsync<string>(
            null!,
            _ => Task.FromResult("exito-tras-contencion"),
            runnerFalso);

        Assert.Equal("exito-tras-contencion", resultado);
        // Aborted garantiza que el commit fue abortado por el servidor sin aplicar cambios,
        // por lo que el reintento de contención es seguro y esperado.
        Assert.Equal(2, invocaciones);
    }

    [Fact]
    public async Task EjecutarAsync_DemostracionRiesgoDuplicacion_SiSeReintentaTrasCommitIncierto()
    {
        // Demostración de riesgo: si una transacción con estado incierto (Unavailable) se reintenta,
        // mutaciones como incrementos de stock o generación de IDs se ejecutan dos veces.
        var contadorMutaciones = 0;
        var idsGenerados = new List<string>();

        Func<FirestoreDb, Func<Transaction, Task<string>>, TransactionOptions, Task<string>> runnerConUnavailable =
            async (db, callback, options) =>
            {
                // Simula que la transacción ejecuta el callback (mutando estado y generando ID)
                await callback(null!);
                // Luego el commit falla con Unavailable en el primer intento (aunque en el servidor pudo haberse aplicado)
                throw new RpcException(new Status(StatusCode.Unavailable, "Transport closed"));
            };

        // Al ejecutar con la política corregida (sin reintento en Unavailable):
        await Assert.ThrowsAsync<RpcException>(() =>
            InsumosTransacciones.EjecutarAsync<string>(
                null!,
                _ =>
                {
                    contadorMutaciones++;
                    idsGenerados.Add(Guid.NewGuid().ToString("N"));
                    return Task.FromResult("mutacion");
                },
                runnerConUnavailable));

        // Con la política segura, el callback solo fue llamado 1 vez:
        Assert.Equal(1, contadorMutaciones);
        Assert.Single(idsGenerados);
    }
}
