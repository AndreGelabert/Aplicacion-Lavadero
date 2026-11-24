using Firebase.Models;
using Firebase.Security;
using Firebase.Services;
using FirebaseLoginCustom.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;
using System.Reflection;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
using Firebase.Tests.Integration;

namespace Firebase.Tests.Controllers;

public class TurnosControllerTests
{
    [Fact]
    public async Task StaleSearchAndVehicleResponsesDoNotOverrideLatestSelection()
    {
        var script = """
            const fs=require('fs'),vm=require('vm'),assert=require('assert/strict');
            class E { constructor(){this.children=[];this.listeners={};this.value='';this.dataset={};this.textContent='';} addEventListener(t,f){this.listeners[t]=f} fire(t){return this.listeners[t]?.({preventDefault(){}})} append(...x){this.children.push(...x)} replaceChildren(...x){this.children=x} add(x){this.children.push(x)} after(x){this.afterNode=x} querySelectorAll(){return []} }
            const ids=['turno-form','turnoPatente','VehiculoId','ClienteId','turnoInfoVehiculo','turnoServiciosPorVehiculo','ClienteTrajoId','ClienteRetiraId','turnoResumen','turnoPatenteResultados','turnoMensaje','Descuento','CuponId','turnoCuponMensaje','turnoPrecio','turnoTiempo'];
            const e=Object.fromEntries(ids.map(i=>[i,new E()]));
            const document={getElementById:i=>e[i],createElement:()=>new E(),createTextNode:t=>({textContent:t})};
            let requests=[]; const fetch=url=>new Promise((resolve,reject)=>requests.push({url,resolve,reject}));
            const ok=data=>({ok:true,json:async()=>data}); const tick=()=>new Promise(setImmediate);
            vm.runInNewContext(fs.readFileSync('wwwroot/js/turnos.js','utf8'),{document,fetch,Option:function(t,v){this.text=t;this.value=v},URLSearchParams,console});
            (async()=>{
              e.turnoPatente.value='A'; let a=e.turnoPatente.afterNode.fire('click');
              e.turnoPatente.value='B'; let b=e.turnoPatente.afterNode.fire('click');
              requests[1].resolve(ok([{id:'b',patente:'B',tipoVehiculo:'Auto',descripcion:'B'}])); await b;
              const latestMessage=e.turnoMensaje.textContent; requests[0].reject(Error('old')); await a; assert.equal(e.turnoMensaje.textContent,latestMessage);
              let vb=e.turnoPatenteResultados.children[0].fire('click'); await tick();
              e.turnoPatente.value='A'; let s=e.turnoPatente.afterNode.fire('click'); requests[5].resolve(ok([{id:'a',patente:'A',tipoVehiculo:'Auto',descripcion:'A'}])); await s;
              let va=e.turnoPatenteResultados.children[0].fire('click'); await tick();
              for(let i=6;i<9;i++) requests[i].resolve(ok(i===6?[{id:'ca',nombre:'A',documento:'1'}]:[])); await va;
              for(let i=2;i<5;i++) requests[i].resolve(ok(i===2?[{id:'cb',nombre:'B',documento:'2'}]:[])); await vb;
              assert.equal(e.VehiculoId.value,'a'); assert.equal(e.ClienteId.value,'ca'); assert.equal(e.turnoInfoVehiculo.textContent,'A'); assert.equal(e.ClienteTrajoId.children.at(-1).value,'ca');
            })().catch(x=>{console.error(x);process.exitCode=1});
            """;
        var start = new System.Diagnostics.ProcessStartInfo("node") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = FindRepositoryRoot() };
        using var process = System.Diagnostics.Process.Start(start)!;
        await process.StandardInput.WriteAsync(script); process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync(); var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); Assert.True(process.ExitCode == 0, $"node exit {process.ExitCode}: {stdout}\n{stderr}");
    }

    [Fact]
    public async Task SummaryShowsPriceAndDurationInSeparateControls()
    {
        var script = """
            const fs=require('fs'),vm=require('vm'),assert=require('assert/strict');
            class E { constructor(){this.value='';this.textContent='';this.listeners={};this.dataset={}} addEventListener(t,f){this.listeners[t]=f} after(){} querySelectorAll(){return this.checked||[]} replaceChildren(){} }
            const ids=['turno-form','turnoPatente','VehiculoId','ClienteId','turnoInfoVehiculo','turnoServiciosPorVehiculo','ClienteTrajoId','ClienteRetiraId','turnoResumen','turnoPrecio','turnoTiempo','turnoPatenteResultados','turnoMensaje','Descuento','CuponId','turnoCuponMensaje'];
            const e=Object.fromEntries(ids.map(i=>[i,new E()]));
            vm.runInNewContext(fs.readFileSync('wwwroot/js/turnos.js','utf8'),{document:{getElementById:i=>e[i],createElement:()=>new E()},URLSearchParams,Option:function(){}});
            e.turnoServiciosPorVehiculo.checked=[{dataset:{price:'100',duration:'30'}},{dataset:{price:'50',duration:'15'}}]; e.Descuento.value='10'; e.Descuento.listeners.input();
            assert.equal(e.turnoPrecio.textContent,'$135.00'); assert.equal(e.turnoTiempo.textContent,'45 min');
            e.turnoPatente.listeners.input(); assert.equal(e.turnoPrecio.textContent,'$0.00'); assert.equal(e.turnoTiempo.textContent,'0 min');
            """;
        var start = new System.Diagnostics.ProcessStartInfo("node") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = FindRepositoryRoot() };
        using var process = System.Diagnostics.Process.Start(start)!;
        await process.StandardInput.WriteAsync(script); process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync(); var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); Assert.True(process.ExitCode == 0, $"node exit {process.ExitCode}: {stdout}\n{stderr}");
    }

    [Fact]
    public void AssociatedClientFormUsesExistingProtectedLavadosRoute()
    {
        var root = FindRepositoryRoot();
        var view = File.ReadAllText(Path.Combine(root, "Views", "Turnos", "_TurnoForm.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "wwwroot", "js", "turnos.js"));
        Assert.Contains("Clientes.Crear", view);
        Assert.Contains("registroClienteTipoDocumento", view);
        Assert.Contains("id=\"turnoPrecio\"", view);
        Assert.Contains("id=\"turnoTiempo\"", view);
        Assert.Contains("max-w-2xl max-h-[90vh]", view);
        Assert.Contains("grid grid-cols-1 gap-4 md:grid-cols-2", view);
        Assert.Contains("flex items-center justify-between gap-2 mb-2", view);
        Assert.Contains("border-t p-4 dark:border-gray-600", view);
        Assert.Contains("/Lavados/RegistrarClienteAsociado", script);
        Assert.Contains("RequestVerificationToken", script);
    }

    [Fact]
    public async Task AssociatedClientDialogUpdatesSelectorsAndIgnoresStaleVehicleResponse()
    {
        var script = """
            const fs = require('fs'), vm = require('vm'), assert = require('assert/strict');
            class Element {
              constructor(tag='') { this.tag=tag; this.children=[]; this.listeners={}; this.dataset={}; this.value=''; this.disabled=false; this.open=false; this.selectedOptions=[{dataset:{regex:'^\\d{3}$',formato:'nnn'}}]; }
              addEventListener(t,f){ this.listeners[t]=f; } async fire(t){ return this.listeners[t]?.({preventDefault(){}}); }
              append(...n){this.children.push(...n);} replaceChildren(...n){this.children=n; if(this.tag==='select') this.value=n[0]?.value||'';} add(n){this.children.push(n);}
              after(n){this.afterNode=n;} querySelectorAll(){return [];} querySelector(s){return s.includes('__Request') ? {value:'csrf'} : this.submit;}
              showModal(){this.open=true;} close(){this.open=false;} setCustomValidity(v){this.validity=v;} reportValidity(){return !elements.registroClienteNumeroDocumento.validity;} reset(){this.resetCalled=true;}
            }
            const ids=['turno-form','turnoPatente','VehiculoId','ClienteId','turnoInfoVehiculo','turnoServiciosPorVehiculo','ClienteTrajoId','ClienteRetiraId','turnoResumen','turnoPatenteResultados','turnoMensaje','Descuento','abrirRegistroCliente','registroClienteForm','registroClienteModal','registroClienteTipoDocumento','registroClienteNumeroDocumento','registroClienteNombre','registroClienteApellido','registroClienteTelefono','registroClienteEmail','registroClienteClave','registroClienteMensaje','cancelarRegistroCliente','CuponId','turnoCuponMensaje','turnoPrecio','turnoTiempo'];
            const elements=Object.fromEntries(ids.map(id=>[id,new Element(id==='ClienteTrajoId'||id==='ClienteRetiraId'?'select':'')]));
            elements.registroClienteForm.submit=new Element();
            Object.assign(elements.registroClienteTipoDocumento,{value:'DNI'}); elements.registroClienteNumeroDocumento.value='456';
            for(const id of ['Nombre','Apellido','Telefono','Email','Clave']) elements['registroCliente'+id].value=id;
            const document={getElementById:id=>elements[id],createElement:t=>new Element(t),createTextNode:t=>({text:t})};
            let resolveClients, resolvePost, posted;
            const response=data=>({ok:true,json:async()=>data});
            const fetch=async (url,options)=>{
              if(options) {posted=options; return new Promise(r=>resolvePost=()=>r(response({success:true,message:'ok',cliente:{id:'new',nombre:'Ana',documento:'DNI: 456'}})));}
              if(url.includes('BuscarVehiculos')) return response([{id:'v',patente:'ABC123',tipoVehiculo:'Auto',descripcion:'auto'}]);
              if(url.includes('ObtenerClientes')) return new Promise(r=>resolveClients=()=>r(response([{id:'old',nombre:'Old',documento:'1'}])));
              return response([]);
            };
            const Option=function(text,value){this.text=text;this.value=value};
            vm.runInNewContext(fs.readFileSync('wwwroot/js/turnos.js','utf8'),{document,fetch,Option,URLSearchParams,console});
            async function select(){ await elements.turnoPatente.afterNode.fire('click'); const pending=elements.turnoPatenteResultados.children[0].fire('click'); await new Promise(setImmediate); return {pending}; }
            (async()=>{
              let {pending}=await select(); assert.equal(elements.abrirRegistroCliente.disabled,true,'registration must wait for selector loading');
              resolveClients(); await pending; assert.equal(elements.abrirRegistroCliente.disabled,false);
              await elements.abrirRegistroCliente.fire('click'); assert.equal(elements.registroClienteModal.open,true);
              let saving=elements.registroClienteForm.fire('submit'); assert.equal(posted.headers.RequestVerificationToken,'csrf'); assert.equal(JSON.parse(posted.body).vehiculoId,'v'); resolvePost(); await saving;
              assert.equal(elements.ClienteTrajoId.value,'new'); assert.equal(elements.ClienteId.value,'new'); assert.equal(elements.ClienteRetiraId.children.at(-1).value,'new'); assert.equal(elements.registroClienteModal.open,false);
              await elements.abrirRegistroCliente.fire('click'); saving=elements.registroClienteForm.fire('submit'); await elements.turnoPatente.fire('input'); resolvePost(); await saving;
              assert.equal(elements.ClienteId.value,''); assert.equal(elements.ClienteTrajoId.children.length,0); assert.equal(elements.ClienteRetiraId.children.length,0);
            })().catch(e=>{console.error(e);process.exitCode=1});
            """;
        var start = new System.Diagnostics.ProcessStartInfo("node") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = FindRepositoryRoot() };
        using var process = System.Diagnostics.Process.Start(start)!;
        await process.StandardInput.WriteAsync(script); process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync(); var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync(); Assert.True(process.ExitCode == 0, $"node exit {process.ExitCode}: {stdout}\n{stderr}");
    }

    [Fact]
    public async Task BrowserScriptSynchronizesSelectedClientAndClearsWhenNoClients()
    {
        var script = """
            const fs = require('fs'), vm = require('vm'), assert = require('assert/strict');
            class Element {
              constructor(tag = '') { this.tag = tag; this.children = []; this.listeners = {}; this.dataset = {}; this._value = ''; }
              set value(v) { this._value = v; }
              get value() { return this._value; }
              addEventListener(type, fn) { this.listeners[type] = fn; }
              async fire(type) { if (this.listeners[type]) await this.listeners[type]({ preventDefault() {} }); if (this['on' + type]) this['on' + type](); }
              append(...nodes) { this.children.push(...nodes); }
              replaceChildren(...nodes) { this.children = nodes; if (this.tag === 'select') this._value = nodes[0]?.value ?? ''; }
              add(node) { this.children.push(node); }
              after(node) { this.afterNode = node; }
              querySelectorAll() { return []; }
            }
            const ids = ['turno-form','turnoPatente','VehiculoId','ClienteId','turnoInfoVehiculo','turnoServiciosPorVehiculo','ClienteTrajoId','ClienteRetiraId','turnoResumen','turnoPatenteResultados','turnoMensaje','Descuento','CuponId','turnoCuponMensaje','turnoPrecio','turnoTiempo'];
            const elements = Object.fromEntries(ids.map(id => [id, new Element(id.startsWith('Cliente') && id !== 'ClienteId' ? 'select' : '')]));
            let clients = [{id:'first',nombre:'First',documento:'1'}, {id:'second',nombre:'Second',documento:'2'}];
            const document = {getElementById: id => elements[id], createElement: tag => new Element(tag), createTextNode: text => ({text})};
            const fetch = async url => ({ok:true, json:async () => url.includes('BuscarVehiculos') ? [{id:'v',patente:'ABC123',tipoVehiculo:'Auto',descripcion:'auto'}] : url.includes('ObtenerClientes') ? clients : []});
            const Option = function(text, value) { this.text = text; this.value = value; };
            vm.runInNewContext(fs.readFileSync('wwwroot/js/turnos.js','utf8'), {document, fetch, Option, URLSearchParams, console});
            async function selectVehicle() {
              await elements.turnoPatente.afterNode.fire('click');
              await elements.turnoPatenteResultados.children[0].fire('click');
              await new Promise(resolve => setImmediate(resolve));
            }
            (async () => {
              await selectVehicle();
              assert.equal(elements.ClienteTrajoId.value, 'first');
              assert.equal(elements.ClienteId.value, 'first');
              elements.ClienteTrajoId.value = 'second'; await elements.ClienteTrajoId.fire('change');
              assert.equal(elements.ClienteId.value, 'second');
              clients = []; await selectVehicle();
              assert.equal(elements.ClienteTrajoId.value, ''); assert.equal(elements.ClienteId.value, '');
              assert.match(elements.turnoMensaje.textContent, /no tiene clientes activos/);
            })().catch(e => { console.error(e); process.exitCode = 1; });
            """;
        var start = new System.Diagnostics.ProcessStartInfo("node") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = FindRepositoryRoot() };
        using var process = System.Diagnostics.Process.Start(start)!;
        await process.StandardInput.WriteAsync(script);
        process.StandardInput.Close();
        var stdout = await process.StandardOutput.ReadToEndAsync();
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"node exit {process.ExitCode}: {stdout}\n{stderr}");
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "wwwroot", "js", "turnos.js"))) dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("Repository root not found");
    }

    [Fact]
    public async Task IndexAndDetailReturnPersistedResultsAndMissingIs404()
    {
        var turno = new TurnoConsultaViewModel("t1", "Ana Perez", "Pendiente", default, default, false, 0, Array.Empty<VehiculoConsultaViewModel>());
        var reader = new Mock<ITurnoConsultaService>();
        reader.Setup(x => x.Listar()).ReturnsAsync(new List<TurnoConsultaViewModel> { turno });
        reader.Setup(x => x.Obtener("t1")).ReturnsAsync(turno);
        reader.Setup(x => x.Obtener("missing")).ReturnsAsync((TurnoConsultaViewModel?)null);
        var controller = new TurnosController(reader.Object);
        var list = Assert.IsType<ViewResult>(await controller.Index());
        Assert.Same(turno, Assert.Single(Assert.IsAssignableFrom<IEnumerable<TurnoConsultaViewModel>>(list.Model)));
        var detail = Assert.IsType<ViewResult>(await controller.Detalle("t1"));
        Assert.Same(turno, detail.Model);
        Assert.IsType<NotFoundResult>(await controller.Detalle("missing"));
    }

    [Firebase.Tests.Integration.LocalFirestoreFact]
    public async Task FirestoreReadsPersistedTurnosWithoutChangingThem()
    {
        var db = new Google.Cloud.Firestore.FirestoreDbBuilder { ProjectId = "demo-turno-read-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly }.Build();
        var entry = new Turno { Id = "one", ClienteId = "client", Estado = "Pendiente", InicioUtc = new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), FinUtc = new DateTime(2030, 1, 1, 12, 30, 0, DateTimeKind.Utc) };
        var reference = db.Collection("turnos").Document(entry.Id);
        await reference.SetAsync(entry);
        var before = await reference.GetSnapshotAsync();
        var controller = new TurnosController(new TurnoConsultaService(db));
        var list = Assert.IsType<ViewResult>(await controller.Index());
        Assert.Equal("one", Assert.Single(Assert.IsAssignableFrom<IEnumerable<TurnoConsultaViewModel>>(list.Model)).Id);
        var detail = Assert.IsType<ViewResult>(await controller.Detalle("one"));
        Assert.Equal("No disponible", Assert.IsType<TurnoConsultaViewModel>(detail.Model).Cliente);
        Assert.IsType<NotFoundResult>(await controller.Detalle("missing"));
        Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
    }

    [Fact]
    public async Task PermissionPolicyRejectsAnonymousAndOtherPermissions()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.LoggingServiceCollectionExtensions.AddLogging(services);
        Microsoft.Extensions.DependencyInjection.PolicyServiceCollectionExtensions.AddAuthorization(services);
        Microsoft.Extensions.DependencyInjection.ServiceCollectionServiceExtensions.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider, PermisoPolicyProvider>(services);
        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        var auth = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>(provider);
        var policy = typeof(TurnosController).GetMethod(nameof(TurnosController.Index))!.GetCustomAttribute<PermisoAttribute>()!.Policy!;
        foreach (var claim in new[] { "Turnos.Crear", "Lavados.Consultar", "" })
        {
            var identity = new System.Security.Claims.ClaimsIdentity(claim == "" ? Array.Empty<System.Security.Claims.Claim>() : new[] { new System.Security.Claims.Claim(PermisoClaim.Tipo, claim) }, claim == "" ? null : "test");
            Assert.False((await auth.AuthorizeAsync(new System.Security.Claims.ClaimsPrincipal(identity), null, policy)).Succeeded);
        }
        var allowed = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(PermisoClaim.Tipo, "Turnos.Consultar") }, "test"));
        Assert.True((await auth.AuthorizeAsync(allowed, null, policy)).Succeeded);
    }

    [Fact]
    public void ReadOnlyViewsUseApplicationTimeZoneResolver()
    {
        var root = FindRepositoryRoot();
        foreach (var file in new[] { "_TurnoTable.cshtml", "Detalle.cshtml" })
        {
            var view = File.ReadAllText(Path.Combine(root, "Views", "Turnos", file));
            Assert.Contains("Firebase.Utils.TimeZoneHelper.ArgentinaZone", view);
            Assert.DoesNotContain("FindSystemTimeZoneById", view);
        }
    }

    [LocalFirestoreFact]
    public async Task HttpEnforcesPermissionAndRendersBuenosAiresTime()
    {
        Assert.Equal("127.0.0.1:8190", Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST"));
        var db = new Google.Cloud.Firestore.FirestoreDbBuilder { ProjectId = "demo-turno-http-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly }.Build();
        var turno = new Turno { Id = "t1", ClienteId = "cliente-http<script>", Estado = "Pendiente<img>", InicioUtc = new DateTime(2030, 1, 1, 2, 0, 0, DateTimeKind.Utc), FinUtc = new DateTime(2030, 1, 1, 2, 30, 0, DateTimeKind.Utc), Vehiculos = new() { new VehiculoEnTurno { VehiculoId = "vehiculo<b>", Servicios = new() { new ServicioEnTurno { ServicioId = "servicio<i>" } } } } };
        var reference = db.Collection("turnos").Document(turno.Id);
        await reference.SetAsync(turno);
        await db.Collection("clientes").Document(turno.ClienteId).SetAsync(new { Nombre = "Ana<script>", Apellido = "Pérez" });
        await db.Collection("vehiculos").Document("vehiculo<b>").SetAsync(new { Patente = "ABC<b>" });
        await db.Collection("servicios").Document("servicio<i>").SetAsync(new { Nombre = "Lavado<i>" });
        var before = await reference.GetSnapshotAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication("Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, InsumosPermisosHttpIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(db);
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<ITurnoConsultaService, TurnoConsultaService>();
        builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>();
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            foreach (var path in new[] { "/Turnos", "/Turnos/Detalle/t1" })
            {
                using var anonymous = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
                client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Crear");
                using var denied = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
                client.DefaultRequestHeaders.Remove("X-Test-Permissions");
            }
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Consultar");
            foreach (var path in new[] { "/Turnos", "/Turnos/Detalle/t1" })
            {
                using var response = await client.GetAsync(path);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var html = await response.Content.ReadAsStringAsync();
                Assert.Contains("31/12/2029 23:00", html);
                Assert.Contains("Ana&lt;script&gt; P&#xE9;rez", html);
                Assert.DoesNotContain("cliente-http", html);
                Assert.Contains("Pendiente&lt;img&gt;", html);
                Assert.DoesNotContain("cliente-http<script>", html);
                Assert.DoesNotContain("Pendiente<img>", html);
                Assert.Contains("bg-gray-50 dark:bg-gray-900", html);
                Assert.DoesNotContain("Registrar turno</a>", html);
                Assert.DoesNotContain("Cancelar turno", html);
                Assert.DoesNotContain("Editar turno", html);
                if (path == "/Turnos")
                {
                    Assert.Contains("aria-label=\"Tabla de turnos\"", html);
                    Assert.Contains("href=\"/Turnos/Detalle/t1\"", html);
                }
                else
                {
                    Assert.Contains("31/12/2029 23:30", html);
                    Assert.Contains("ABC&lt;b&gt;", html);
                    Assert.Contains("Lavado&lt;i&gt;", html);
                    Assert.DoesNotContain("vehiculo<b>", html);
                    Assert.DoesNotContain("servicio<i>", html);
                    Assert.Contains("Información General", html);
                    Assert.Contains("Volver a turnos", html);
                }
            }
            client.DefaultRequestHeaders.Remove("X-Test-Permissions");
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Consultar,Turnos.Crear");
            var withCreate = await client.GetStringAsync("/Turnos");
            Assert.Contains("href=\"/Turnos/Crear\"", withCreate);
            Assert.Contains("Registrar turno", withCreate);
            Assert.Equal(before.UpdateTime, (await reference.GetSnapshotAsync()).UpdateTime);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task SelectorHttpPermissionMatrixAndNoPost()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication("Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, InsumosPermisosHttpIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(Mock.Of<ITurnoConsultaService>());
        var selector = new Mock<ITurnoSelectorService>(MockBehavior.Strict);
        selector.Setup(x => x.BuscarVehiculos("x", It.IsAny<bool>())).ReturnsAsync(Array.Empty<object>());
        selector.Setup(x => x.ClientesVehiculo("x", null, It.IsAny<bool>())).ReturnsAsync(Array.Empty<object>());
        selector.Setup(x => x.Servicios(null)).ReturnsAsync(Array.Empty<object>());
        selector.Setup(x => x.Paquetes(null)).ReturnsAsync(Array.Empty<object>());
        builder.Services.AddSingleton(selector.Object);
        await using var app = builder.Build();
        app.UseDeveloperExceptionPage();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            foreach (var path in new[] { "/Turnos/BuscarVehiculosPorPatente?search=x", "/Turnos/ObtenerClientesVehiculo?vehiculoId=x", "/Turnos/ObtenerServiciosPorTipoVehiculo", "/Turnos/ObtenerPaquetesPorTipoVehiculo" })
            {
                Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
                client.DefaultRequestHeaders.Add("X-Test-Permissions", "Lavados.Consultar");
                Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
                client.DefaultRequestHeaders.Remove("X-Test-Permissions");
                foreach (var permission in new[] { "Turnos.Crear", "Turnos.Consultar" })
                {
                    client.DefaultRequestHeaders.Add("X-Test-Permissions", permission);
                    using var response = await client.GetAsync(path);
                    Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} ({permission}): {response.StatusCode}\n{await response.Content.ReadAsStringAsync()}");
                    Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.PostAsync(path, null)).StatusCode);
                    client.DefaultRequestHeaders.Remove("X-Test-Permissions");
                }
            }
        }
        finally { await app.StopAsync(); }
    }

    [LocalFirestoreFact]
    public async Task CreateFormRequiresCreatePermissionAndPostRequiresAntiforgery()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication("Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, InsumosPermisosHttpIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(Mock.Of<ITurnoConsultaService>());
        builder.Services.AddSingleton<TurnoFormIdentity>();
        builder.Services.AddSingleton(new Google.Cloud.Firestore.FirestoreDbBuilder { ProjectId = "demo-turno-create-" + Guid.NewGuid().ToString("N")[..12], EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOnly }.Build());
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<ConfiguracionService>();
        builder.Services.AddScoped<InsumoService>();
        await using var app = builder.Build();
        app.UseDeveloperExceptionPage();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/Turnos/Crear")).StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Consultar");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/Turnos/Crear")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Permissions");
            client.DefaultRequestHeaders.Add("X-Test-Permissions", "Turnos.Crear");
            using var page = await client.GetAsync("/Turnos/Crear");
            var html = await page.Content.ReadAsStringAsync();
            Assert.True(page.StatusCode == HttpStatusCode.OK, $"GET /Turnos/Crear: {page.StatusCode}\n{html}");
            Assert.Contains("__RequestVerificationToken", html);
            Assert.Contains("FormIdentity", html);
            Assert.Contains("class=\"relative\"", html);
            Assert.Contains("absolute z-50 w-full bg-white dark:bg-gray-700", html);
            Assert.Contains("Información del Vehículo", html);
            Assert.Contains("grid gap-4 grid-cols-1 md:grid-cols-3", html);
            Assert.Contains("Precio Total", html);
            Assert.Contains("Tiempo Estimado", html);
            foreach (var id in new[] { "turnoPatente", "turnoPatenteResultados", "turnoInfoVehiculo", "ClienteTrajoId", "ClienteRetiraId", "turnoServiciosPorVehiculo", "turnoResumen", "Fecha", "Hora", "AceptaAdelanto" })
                Assert.Contains($"id=\"{id}\"", html);
            Assert.Contains("turnos.js", html);
            Assert.DoesNotContain("El formulario de registro aún no está disponible", html);
            Assert.Contains("type=\"submit\"", html);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Turnos/Crear", new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>()))).StatusCode);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task ModificationAndCancellationHttpRequireConsultAndActionPermissions()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ApplicationName = typeof(TurnosController).Assembly.GetName().Name, EnvironmentName = "Development" });
        builder.WebHost.ConfigureKestrel(o => o.Listen(IPAddress.Loopback, 0));
        builder.Services.AddControllersWithViews();
        builder.Services.AddAuthentication("Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, InsumosPermisosHttpIntegrationTests.HeaderAuth>("Test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        builder.Services.AddSingleton(Mock.Of<ITurnoConsultaService>());
        await using var app = builder.Build();
        app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single()) };
            foreach (var (path, post, action) in new[] { ("/Turnos/EditarToken", false, "Turnos.Modificar"), ("/Turnos/Editar", true, "Turnos.Modificar"), ("/Turnos/RecursosInicio", false, "Turnos.Modificar"), ("/Turnos/Iniciar", true, "Turnos.Modificar"), ("/Turnos/Cancelar", true, "Turnos.Cancelar") })
            {
                async Task<HttpStatusCode> Send(string permissions)
                {
                    client.DefaultRequestHeaders.Remove("X-Test-Permissions");
                    if (permissions.Length > 0) client.DefaultRequestHeaders.Add("X-Test-Permissions", permissions);
                    using var response = post ? await client.PostAsync(path, new FormUrlEncodedContent(Array.Empty<KeyValuePair<string, string>>())) : await client.GetAsync(path);
                    return response.StatusCode;
                }
                Assert.Equal(HttpStatusCode.Unauthorized, await Send(""));
                Assert.Equal(HttpStatusCode.Forbidden, await Send(action + ",Lavados.Crear,Lavados.Modificar"));
                Assert.Equal(HttpStatusCode.Forbidden, await Send("Turnos.Consultar,Lavados.Crear,Lavados.Modificar"));
                Assert.Equal(HttpStatusCode.BadRequest, await Send("Turnos.Consultar," + action + ",Lavados.Crear,Lavados.Modificar"));
                if (path == "/Turnos/RecursosInicio")
                    Assert.Equal(HttpStatusCode.Forbidden, await Send("Turnos.Consultar,Turnos.Modificar"));
            }
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public void DetailActionButtonsAndResourcesRequireConsultAndPreserveExistingGuards()
    {
        var view = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Views", "Turnos", "Detalle.cshtml"));
        var editGuard = "@if (Model.Estado == \"Pendiente\" && User.TienePermiso(\"Turnos.Consultar\") && User.TienePermiso(\"Turnos.Modificar\"))";
        Assert.Contains(editGuard, view);
        Assert.Contains("Editar turno", view);
        foreach (var action in new[]
        {
            (Guard: "User.TienePermiso(\"Turnos.Cancelar\")", Button: "data-turno-cancelacion", Partial: "_TurnoCancelacion", Script: "turno-cancelacion.js"),
            (Guard: "User.TienePermiso(\"Turnos.Modificar\") && User.TienePermiso(\"Lavados.Crear\") && User.TienePermiso(\"Lavados.Modificar\")", Button: "data-turno-inicio", Partial: "_TurnoInicio", Script: "turno-inicio.js")
        })
        {
            var guard = "@if (Model.Estado == \"Pendiente\" && User.TienePermiso(\"Turnos.Consultar\") && " + action.Guard + ")";
            Assert.Equal(2, view.Split(guard).Length - 1);
            var sections = view.Split(guard);
            Assert.Contains(action.Button, sections[1]);
            Assert.Contains(action.Partial, sections[2]);
            Assert.Contains(action.Script, sections[2]);
        }
        Assert.Equal(1, view.Split("@if (!string.IsNullOrEmpty(Model.VersionEsperada))").Length - 1);
    }

    [Fact]
    public async Task CancellationPolicyRequiresExplicitPermission()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<IAuthorizationPolicyProvider, PermisoPolicyProvider>();
        using var provider = services.BuildServiceProvider();
        var auth = provider.GetRequiredService<IAuthorizationService>();
        var policy = typeof(TurnosController).GetMethod(nameof(TurnosController.Cancelar))!.GetCustomAttribute<PermisoAttribute>()!.Policy!;
        foreach (var permission in new[] { "Turnos.Modificar", "Turnos.Consultar", "Turnos.Cancelar" })
        {
            var user = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(PermisoClaim.Tipo, permission) }, "test"));
            Assert.Equal(permission == "Turnos.Cancelar", (await auth.AuthorizeAsync(user, null, policy)).Succeeded);
        }
        var view = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "Views", "Turnos", "Detalle.cshtml"));
        Assert.Contains("User.TienePermiso(\"Turnos.Cancelar\")", view);
    }

    [Fact]
    public async Task CancellationValidatesIdsLikeStartBeforeResolvingService()
    {
        var controller = new TurnosController(Mock.Of<ITurnoConsultaService>());
        var invalid = new string?[] { null, "", " ", "/", ".", "..", " padded", "padded ", "a\nb", "__reserved__", new string('a', 1501), new string('é', 751) };
        foreach (var id in invalid)
        {
            if (id != "__reserved__") Assert.False(new TurnoInicioRequest { TurnoId = id }.EsValido());
            var result = Assert.IsType<BadRequestObjectResult>(await controller.Cancelar(id));
            Assert.Equal("{ success = False, message = Datos de turno inválidos. }", result.Value!.ToString());
        }
    }

    [Fact]
    public async Task CancellationValidIdsReachServiceResolutionAndKeepSecurityAttributes()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var controller = new TurnosController(Mock.Of<ITurnoConsultaService>())
        {
            ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { RequestServices = services } }
        };
        foreach (var id in new[] { "valid-id", new string('a', 1500), new string('é', 750) })
        {
            Assert.True(new TurnoInicioRequest { TurnoId = id }.EsValido());
            var result = Assert.IsType<ObjectResult>(await controller.Cancelar(id, "Motivo válido"));
            Assert.Equal(503, result.StatusCode);
            Assert.Equal("{ success = False, message = No se pudo cancelar el turno. }", result.Value!.ToString());
        }
        var action = typeof(TurnosController).GetMethod(nameof(TurnosController.Cancelar))!;
        Assert.Equal(new[] { "Turnos.Cancelar" }, action.GetCustomAttribute<PermisoAttribute>()!.Permisos);
        Assert.NotNull(action.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(action.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public void AllActionsRequireExactConsultPermissionAndAreReadOnly()
    {
        var actions = typeof(TurnosController).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        var create = actions.Where(a => a.Name is "Crear" or "CrearPost").ToArray();
        Assert.Equal(2, create.Length);
        Assert.All(create, a => Assert.Equal(new[] { "Turnos.Crear" }, a.GetCustomAttribute<PermisoAttribute>()!.Permisos));
        Assert.NotNull(create.Single(a => a.Name == "CrearPost").GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        var inicio = Assert.Single(actions.Where(a => a.Name == "Iniciar"));
        Assert.Equal(new[] { "Turnos.Modificar" }, inicio.GetCustomAttribute<PermisoAttribute>()!.Permisos);
        Assert.NotNull(inicio.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(inicio.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        var cancelacion = Assert.Single(actions.Where(a => a.Name == "Cancelar"));
        Assert.Equal(new[] { "Turnos.Cancelar" }, cancelacion.GetCustomAttribute<PermisoAttribute>()!.Permisos);
        Assert.NotNull(cancelacion.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(cancelacion.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        var edit = Assert.Single(actions.Where(a => a.Name == "Editar"));
        var editToken = Assert.Single(actions.Where(a => a.Name == "EditarToken"));
        Assert.Equal(new[] { "Turnos.Modificar" }, edit.GetCustomAttribute<PermisoAttribute>()!.Permisos);
        Assert.NotNull(edit.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.Equal(new[] { "Turnos.Modificar" }, editToken.GetCustomAttribute<PermisoAttribute>()!.Permisos);
        var recursos = Assert.Single(actions.Where(a => a.Name == "RecursosInicio"));
        Assert.Equal(new[] { "Turnos.Modificar" }, recursos.GetCustomAttribute<PermisoAttribute>()!.Permisos);
        Assert.NotNull(recursos.GetCustomAttribute<HttpGetAttribute>());
        var datosEdicion = Assert.Single(actions.Where(a => a.Name == "DatosEdicion"));
        Assert.Equal(new[] { "Turnos.Modificar" }, datosEdicion.GetCustomAttribute<PermisoAttribute>()!.Permisos);
        Assert.NotNull(datosEdicion.GetCustomAttribute<HttpGetAttribute>());
        actions = actions.Except(create).Except(new[] { inicio, cancelacion, edit, editToken, recursos, datosEdicion }).ToArray();
        Assert.Equal(new[] { "BuscarVehiculosPorPatente", "Detalle", "Index", "ObtenerClientesVehiculo", "ObtenerPaquetesPorTipoVehiculo", "ObtenerServiciosPorTipoVehiculo", "TablePartial" }, actions.Select(m => m.Name).OrderBy(n => n).ToArray());
        Assert.All(actions, action =>
        {
            Assert.NotNull(action.GetCustomAttribute<HttpGetAttribute>());
            var permission = action.GetCustomAttribute<PermisoAttribute>();
            Assert.NotNull(permission);
            Assert.Equal(action.Name is "Index" or "Detalle" or "TablePartial" ? new[] { "Turnos.Consultar" } : new[] { "Turnos.Crear", "Turnos.Consultar", "Turnos.Modificar" }, permission!.Permisos);
        });
    }
}
