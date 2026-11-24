using System.Diagnostics;

namespace Firebase.Tests.Controllers;

public class TurnoCuponSelectorTests
{
    [Fact]
    public async Task SelectorTracksClientRejectsStaleResponsesAndPreservesForm()
    {
        var script = """
            const fs=require('fs'),vm=require('vm'),assert=require('assert/strict');
            class E {
              constructor(){this.children=[];this.listeners={};this.value='';this.dataset={};this.textContent='';this.disabled=false;}
              addEventListener(t,f){this.listeners[t]=f} fire(t){return this.listeners[t]?.({preventDefault(){}})}
              append(...x){this.children.push(...x)} replaceChildren(...x){this.children=x;this.value=x[0]?.value||''}
              add(x){this.children.push(x)} after(x){this.afterNode=x} querySelectorAll(){return []}
            }
            const ids=['turno-form','turnoPatente','VehiculoId','ClienteId','turnoInfoVehiculo','turnoServiciosPorVehiculo','ClienteTrajoId','ClienteRetiraId','turnoResumen','turnoPatenteResultados','turnoMensaje','Descuento','CuponId','turnoCuponMensaje','turnoPrecio','turnoTiempo','Notas'];
            const e=Object.fromEntries(ids.map(id=>[id,new E()]));
            let pending=[];
            const ok=data=>({ok:true,json:async()=>data});
            const fetch=url=>url.includes('Consultar?') ? new Promise((resolve,reject)=>pending.push({url,resolve,reject})) : Promise.resolve(ok(url.includes('BuscarVehiculos')?[{id:'v',patente:'ABC',tipoVehiculo:'Auto',descripcion:'auto'}]:url.includes('ObtenerClientes')?[{id:'a',nombre:'A',documento:'1'},{id:'b',nombre:'B',documento:'2'}]:[]));
            const document={getElementById:id=>e[id],createElement:()=>new E(),createTextNode:text=>({text})};
            vm.runInNewContext(fs.readFileSync('wwwroot/js/turnos.js','utf8'),{document,fetch,Option:function(text,value){this.text=text;this.value=value},URLSearchParams,console});
            const tick=()=>new Promise(setImmediate);
            const watchdog=setTimeout(()=>{console.error('diagnostic timeout');process.exit(124)},5000);
            (async()=>{
              e.Notas.value='conservar'; await e.turnoPatente.afterNode.fire('click'); e.turnoPatenteResultados.children[0].fire('click'); await tick();
              assert.equal(pending.length,1); assert.match(pending[0].url,/clienteId=a/);
              e.ClienteTrajoId.value='b'; e.ClienteTrajoId.onchange(); await tick(); assert.equal(pending.length,2);
              pending[1].resolve(ok([{id:'b-coupon',percentage:20,expiresUtc:'2030-01-01T00:00:00Z'}])); await tick();
              assert.equal(e.CuponId.children.at(-1).value,'b-coupon');
              pending[0].resolve(ok([{id:'a-coupon',percentage:10,expiresUtc:'2030-01-01T00:00:00Z'}])); await tick();
              assert.equal(e.CuponId.children.at(-1).value,'b-coupon'); assert.equal(e.ClienteId.value,'b');
              e.Descuento.value='7'; e.CuponId.value='b-coupon'; e.CuponId.fire('change'); assert.equal(Number(e.Descuento.value),0);
              e.Descuento.value='5'; e.Descuento.fire('input'); assert.equal(e.CuponId.value,''); assert.equal(e.Notas.value,'conservar');
              e.ClienteTrajoId.value='a'; e.ClienteTrajoId.onchange(); await tick(); pending[2].reject(Error('offline')); await tick();
              assert.match(e.turnoCuponMensaje.textContent,/no se pudieron/i); assert.equal(e.Notas.value,'conservar');
              e.ClienteTrajoId.value='b'; e.ClienteTrajoId.onchange(); await tick();
              e.turnoPatente.fire('input'); pending[3].resolve(ok([{id:'stale',percentage:10,expiresUtc:'2030-01-01T00:00:00Z'}])); await tick();
              assert.equal(e.CuponId.value,''); assert.equal(e.ClienteId.value,''); assert.equal(e.CuponId.children.length,1);
              assert.equal(e.Notas.value,'conservar'); console.log('PASS selector races, conflict and form preservation');
            })().catch(error=>{console.error(error);process.exitCode=1}).finally(()=>clearTimeout(watchdog));
            """;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "wwwroot", "js", "turnos.js"))) dir = dir.Parent;
        Assert.NotNull(dir);
        var start = new ProcessStartInfo("node") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = dir.FullName };
        using var process = Process.Start(start)!;
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var timedOut = false;
        try
        {
            await process.StandardInput.WriteAsync(script.AsMemory(), timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            timedOut = true;
            try { process.Kill(); }
            catch (InvalidOperationException) { /* The child exited as the timeout elapsed. */ }
            await process.WaitForExitAsync();
        }
        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        Assert.False(timedOut, $"node timed out after 30 seconds: {stdout}\n{stderr}");
        Assert.True(process.ExitCode == 0, $"node exit {process.ExitCode}: {stdout}\n{stderr}");
    }
}
