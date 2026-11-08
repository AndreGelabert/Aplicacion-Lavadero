# Lavados partial listing refactor
Objective: consolidate identical partial-listing metadata while preserving observable behavior.
Work line: refactor/clientes-queries; slice base 9f1a4d3. Local only, no publishing or new branches.

## Specs
S1. "Continua con lo que falta y/o el siguiente modulo" — next bounded consolidation is Lavados partial MVC metadata.
S2. "Siempre valora mas el codigo, o no sigas super a raja tabla la documentacion" — preserve current public signatures, routes, permissions, queries, defaults, pagination and ViewBag keys. SearchTerm remains SearchPartial-only. Index and service/write/payment/lifecycle behavior outside the original partial-listing slice; additional responsibility slices follow S4.
S3. "muchas se hicieron en fases tempranas de desarrollo y despues no se actualizaron mas" — characterize actual public controller actions against disposable Firestore before extraction.

S4. "Bueno, podes seguir con la revision de RDD para que le de por aprobada y avanzar con las advertencias o el siguiente modulo, no se que toca pero quiero que avances" — continue authorized responsibility separation and duplication removal in Lavados after Vehiculos, preserving functional rules.

S5. "Continua por favor" — continue the already authorized refactor; preserve real observable creation pricing and discount behavior.

## Tasks
T1 | S1-S3 | worker + parent isolated checks | done | Existing public partial characterization included fulljob18:864passed0failed0skipped. Commit pending.
T2 | S2-S3 | inline + parent isolated checks | done | Extract identical metadata only, run same suite, native assessment and local commit. Commit: pending.

T3 | S2-S4 | inline + checks | done | Characterize and extract pickup paid/cancelled eligibility and state/date mutation, preserving audit/catch HTTP. Baseb4f4fc8.

T4 | S2-S5 | bounded worker + parent verification | done | Characterize public creation pricing/discounts, extract exact calculation into concrete service without behavior changes. Base323ea02. Commit pending.

T5 | S2-S5 | bounded worker + parent verification | done | Characterize/extract staff requirement default and configured cap plus assignment, preserve employee order/random existing behavior. Basea2b5e6f.

T6 | S2-S5 | bounded worker + parent verification | done | Characterize and separate service/stage assembly and initial start orchestration without changing order or writes. Base9cea159.

T7 | S2-S5 | bounded worker + parent verification | done | Characterize remaining creation guards and multi-vehicle partial writes; then cohesive global/per-vehicle creation service preserving interleaved audit. Basee65638d.

T8 | S2-S5 | bounded worker + parent verification | done | Characterize and move associated-client registration validation/orchestration, preserve prechecks and transactional rechecks exactly. Base647420a.

## Log
L1. User verbatim: "Continua con lo que falta y/o el siguiente modulo"
L2. Explorer mw4zf49k-1-xrqi identified Lavados query/MVC duplication; parent spot-check Controllers/LavadosController.cs:137-238 narrowed scope to exact common ViewBag assignments only. Keep distinct listing/count query paths and SearchTerm absence in TablePartial; no service query unification or Index changes. Baseline previous unit 854 passed; Vehiculos native review approved acknowledged review-d7087452e0476083.

L3. User authorized complete both responsibility and duplication, orderAuditoria/Clientes/Vehiculos/Lavados; now Vehiculos scope completed with native approved units. Explorermw5ew6pc-t-fdny selects RegistrarRetiro743-784 business rules as first responsibility slice, no constructor changes. No pricing/discount/lifecycle semantic change. Existing LavadoQueryRefactorTests remains separate pending metadata extraction. Baseline864full suite.

L4. Pickup baselinejob19 failed compileCS9035 (new fixture missing requiredClienteId/VehiculoId), zero tests run; corrected only fixture. Baselinejob20exit0:865passed0failed0skipped. Exact RegistrarRetiro business body moved to additive LavadoService.RegistrarRetiro + plain ResultadoRetiro(Error,CamposAntes); no constructor/DI changes, existing service methods unchanged, controller keeps permissions/audit/catch/JSON. Parent check caught tool read-truncated large-file writes deleting remote sections in both files; restored complete HEAD bytes and grafted ONLY the understood endpoint/newservice blocks before tests. Corrected scoped diff65lines+39testlines; diff-checkexit0. Full postjob21 launched pending. No assertion changes or business fixes.

L5. Pickup T3done022e11c baseb4f4fc8,3paths102difflines: fullbaselinejob20/postjob21exit0 each865passed0failed0skipped. Actual diff preserves prior guards and service helpers; Risk:none (no auth/concurrency/contract changes; additive domain operation only). Nativeassess root refused untracked scope; clean registered.local/review-wash-pickup assess exact committedbaseb4f4fc8 medium reviewDuefalse under_budget, no independentverifier required. No transaction started per native entry rule.
L6. T2 understood explorer mw5f6cq3-u-y6hv exact identical14ViewBag assignments in partials extracted only to privatePrepararMetadataPartial; SearchTerm stays SearchPartial-only and query/count paths unchanged, Index untouched. Existing untracked LavadoQueryRefactorTests in fullbaseline865. Postjob22running; next pricing/create characterization lacks direct integration coverage per explorer, no production price changes yet.

L7. Metadata postjob22exit1 compilationCS0103: helper incorrectly inserted into siblingServicioSeleccionadoDTO rather than LavadosController. No tests ran. Moved exact helper before controllerRegistrarLavado; no assignments,queries,assertions changed; diff-checkexit0. Corrected postjob23running. T2 remainsin_progress, not complete pending runtime.

L8. Metadata postjob23exit0:865passed0failed0skipped. T2 committed323ea02 (controller+publicLavadoQueryRefactorTests); base022e11c. Prior job22 build failure remains recorded.
L9. User verbatim: "Continua por favor". Context backstop routes next bounded pricing unit to worker. No new behavior, business guards, publishing or branch changes authorized. Parent owns full isolated suites (ports8190/9199), no concurrent emulator.

L10. Pricing baselinejob1exit1:867total866passed1failed; worker source diagnosed employee fixture NombreCompleto vs mapper Nombre, corrected fixture only plus JSON failure diagnostic, arithmetic unchanged. Baselinejob2exit0:867passed0failed0skipped confirms fix. Workermw6k488b-3-b3yy now extracted additive LavadoService.CalcularPrecioCreacion and plainResultadoPrecioCreacion, exact package ordinal filtering, Distinct order, TryCalcularPrecioSeleccionado and decimal package+individual+globaldiscount operation order. Controller retains gates,response,persistence,audit,catch. Scoped parent diff readback unchanged existing helpers/public action contract; no cross-command differential because only additive helper/callsite. Risk:none. Parent postjob3running; T4 NOT done. 2productionfiles48add36delete plus new characterization. Baseline and post source distinct.

L11. T4done a2b5e6f base323ea02 3paths199difflines (tests included). Baselinejob2 andpostjob3 both867passed0failed0skippedexit0; nativeassess clean registered.local/review-wash-pricing medium reviewDuefalse under_budget independentVerifierfalse. No review transaction started.
L12. Explorer mw6k7pod-4-5md4 identifies next bounded staff policy in creation395-405. Default required1, cap onlyifgreater than configmax, existing assignment method unchanged. Pass nullable count to additive service; type lookup stays controller for no constructor changes. Module NOTcomplete: creation service/stage assembly and initial service/stage transitions remain. No new policy inferred.

L13. T5 phase1 workermw6kavwd-5-kkfc added public creation cases missingtype/default1, above4cap2, below2cap3,equal2cap2, persistedrequiredcount/IDs/names/no randomness order. Parent baselinejob4exit0:868passed0failed0skipped. Phase2workermw6kdcf1-6-hjpm extracted additivePrepararAsignacionCreacion(int?) andplainResultadoAsignacionCreacion; type lookup stays controller, default??1/configawait/>onlycap/assignment exact. Parent scoped production diff readback preserved existing helpers/no sibling differential needed. Native assess dirtyroot unassessable untracked selection (not nativeapproval), planselfverificationnoindependentVerifier. Postjob5running; T5 notdone. Risk:none additive operation and controlleronlycallsite; not changing random method/concurrency/persistence logic.

L14. T5done9cea159 basea2b5e6f3paths77difflines. Baselinejob4/postjob5 both868passed0failed0skipexit0. Clean registered.local/review-wash-staff nativeassessmediumreviewDuefalseunder_budget; no independentverifier or native transaction. Next T6 cohesive assembly/start boundary, no new rules or transactions; actor/audit and JSON remain HTTP.

L15. T6 publicbaselinejob6exit0:869passed0failed0skipped. Worker initial assembly tuple extraction moved DTO property reads beforeawait; deferred callbacks proposed preservation but parent rejected callbacks-per-property overengineering. Revised scope additivepureCrearServicioEnLavado(Servicio,int,string?,string?) owns model/stagecopy and IniciarPrimerServicioCreacion owns existingseparatestartcalls. Controller sequentiallookup/compatguard/orderincrement/duration remain exact. No shared helper changes or newconcurrency; package arguments read postlookup no await between parameterreads/modelproperties. Workermw6ko4mo-a-tjww finaldiff3files120add39delete (characterizationincluded). Parent postjob7running. T6notdone/moduleNOTcomplete; remaining eligibility/assemblycoordination assess afterward.

L16. T6donee65638d base9cea1593paths159difflines, publicbaselinejob6/postjob7 each869passed0failed0skipexit0. Clean registered.local/review-wash-assembly nativeassessmediumreviewDuefalseunder_budget. No review started.
L17. Explorermw6krmbl-b-iszp recommends cohesive globalprevalidation/pervehiclecreation service (controller foreach+audit preserved), no callback/atomicity changes. Before routing implementation need characterize missingvehicle/service/compat errors + secondvehicle failure retains firstwash/audit; existing tests cover price/staff/start. Explorer outsidecreation claims unverified broader guard areas; inspect before changing, do not assume payment/lifecycle need business changes.

L18. T7done647420a basee65638d9paths436difflines. Baselinejob8/postjob9 each872passed0failed0skipexit0. Nativeassesshigh dueSecuritytestfactoryadaptations; review-3115a6d83eaec31a risk/resilience/reliability approved exactackburned target6b7f50e6ff5dad89497edf1fe5c25130e63736a2a175e9418fbb866e3f56972c consumedrevision82d48893abcea1b4515a7e1e7af5a6e5ede5f8c12e042ad75ce79bfdae8a14c1. Initialabbreviatedbasestart rejected lineage_createdfalse, fullhash accepted. Noauthguardchanged.
L19. Explorermw6lehtm-e-i1hz actual inventory corrects priorfalseassumption: lifecycle/payment already HTTPadapters, no need extract those. RegistrarClienteAsociado remaining crossentitybusiness boundary. DO NOT eliminate existing pretransaction checks despite explorersuggestion: sourceerrortiming/transactionrechecks preserved mandatory. Lastscope afterthat reconfirmcontroller nonHTTP rules; no new guards/date/warnings.

L20. T8done532d46a base647420a9paths336difflines. Baselinejob10/postjob11exit0 each874passed0failed0skipped. Risk2 keyeligibility relocation independentread-only mw6lq8jk-h-6b76 S4/S5 structurallyMET guards/order/transactionunchanged; no independentruntime. S2 allegedcompileblock refuted parentactualdiff all7ctorcalls include newarg and fullbuild/suiteexit0, no sourcecorrection/retry. Report preserved not relabeled verifierpass.
L21. Native review-0de45d4a32b01aa5 risk/reliability approved exactackburned targetdee2b4a2298a8d9e78e235d22c9832e6db971ee5286a8a0451412f465c801ec6 consumedrevision63222ec24cdc1ebb217ee66e9c7172a4d7f7a950a87fb059e85b98e74c91ef5f. Scoped creation/pickup/staff/materialization/association responsibilities plus partialmetadata duplication complete. Existing payment/lifecycle already serviceowned, retain HTTP auditadapters. No blanket cleanup/guards/pricingfix/transactionchange.
L22. Close bounded module sequence Auditoria/Clientes/Vehiculos/Lavados with874functionaltests and sliceRDD where due. Historicalunavailable independentruns remain unavailable, priorfixturecompile/baselinefailures documented. Browserfixtures historical10passed not rerun for these backendonlychanges, no authenticatedRazorE2E claim.299warnings unclassified and auditinverteddate bug separate pendingitems outside thisrefactor; no claimfixed.
