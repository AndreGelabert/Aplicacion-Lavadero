# Documentación de Testing y Aseguramiento de Calidad - LavaFácil

## 1. Resumen Ejecutivo

Este documento constituye el informe técnico y ejecutivo del estado actual del aseguramiento de calidad (QA) y la infraestructura de pruebas del sistema **LavaFácil**. Reemplaza las estimaciones históricas anteriores para reflejar con fidelidad la totalidad del código de prueba existente en el repositorio, incluyendo las pruebas agregadas recientemente para el **Módulo de Notificaciones al Cliente** (`ClienteNotificationService`) y las preferencias de entrega en cotizaciones de insumos.

### 1.1 Tipos de Pruebas Existentes

El repositorio cuenta con una estrategia de pruebas estructurada en niveles complementarios:

1. **Pruebas Unitarias de Backend (.NET 9 / xUnit)**:
   - Verifican reglas de negocio, modelos de datos, DTOs, cálculos de precios, duraciones, validaciones de expresiones regulares, normalización de datos y contratos de servicios sin dependencias externas, utilizando dobles de prueba con **Moq** y memoria volátil.
2. **Pruebas de Integración con Emuladores Locales (.NET 9 / Firestore / Auth)**:
   - Verifican operaciones de persistencia transaccional, consultas complejas, ordenamiento, filtrado multifiltro, paginación, concurrencia (guardias por patente) y flujos de negocio contra instancias locales y desechables de los emuladores de **Google Cloud Firestore** y **Firebase Auth**.
3. **Pruebas del Módulo de Notificaciones al Cliente (WhatsApp & Email)**:
   - Verifican la construcción de mensajes contextuales veraces según el evento del lavado (etapa finalizada con anticipación de la próxima etapa, servicio finalizado, lavado completado con vehículo listo para retiro, y cancelaciones con motivo explicativo), despacho dual (Meta WhatsApp Cloud API y Resend Email), normalización de preferencias del cliente (requiriendo al menos un canal activo), prevención de reenvíos duplicados (idempotencia) y resiliencia operativa ante contingencias de auditoría.
4. **Pruebas de Seguridad y Control de Acceso Basado en Roles (RBAC)**:
   - Verifican la matriz de permisos, protección contra bypass de autorización, directivas de retención y expiración de sesiones, mitigación de ataques de enumeración y denegación de servicio en restablecimiento de contraseñas, diagnóstico estructurado de transporte sin filtración de secretos, y regeneración de navegación según claims.
5. **Pruebas Unitarias y de Integración de Frontend (Node.js / `node:test`)**:
   - Prueban la lógica de scripts cliente (`wwwroot/js/*.js`), interacción con modales, normalización de formularios, contrastes y consistencia de atributos HTML mediante un entorno DOM simulado con `node:vm`, sin necesidad de levantar un navegador completo.
6. **Pruebas de Interfaz y Componentes en Navegador (Playwright / Chromium)**:
   - Verifican el comportamiento visual e interactivo de vistas y controles (trampas de foco, accesibilidad por teclado, superposición de capas y modales, toasts de notificación, temas claro/oscuro) utilizando Chromium headless sobre un servidor de fixtures locales con respuestas simuladas.
7. **Prueba End-to-End Autenticada con Servidor en Vivo (Playwright + Kestrel + Emuladores)**:
   - Suite especializada que levanta la aplicación ASP.NET Core completa junto a emuladores locales sembrados para validar el ciclo completo de autenticación, revocación de roles en vivo, invalidación de menú y redirección HTTP 403.

### 1.2 Áreas Funcionales Cubiertas

El inventario de pruebas existente cubre los siguientes módulos del sistema:

- **Notificaciones al Cliente**: Despacho automático y multicanal (WhatsApp y Email) por eventos operativos del lavado, cálculo de etapa siguiente, prevención de mensajes engañosos en cancelaciones, normalización estricta de preferencias (`NotificarPorWhatsApp`, `NotificarPorEmail`), diagnóstico seguro de transporte en Meta WhatsApp sin fuga de tokens o datos sensibles, e idempotencia ante eventos repetidos.
- **Clientes**: Registro, ciclo de vida, asociación a vehículos, normalización de datos personales y preferencias de comunicación, y búsqueda insensible a mayúsculas.
- **Vehículos y Catálogo**: Detección de tipos de vehículo (CarQuery y catálogo local), propuestas de nuevos modelos, validación y generación de claves de asociación (algoritmo SHA-256), titularidad, vehículos huérfanos y guardias transaccionales de patentes.
- **Lavados y Servicios**: Estados del ciclo de lavado, cálculo de precios y descuentos, asignación de empleados, etapas operativas, recetas de insumos teóricas vs. reales, y cancelación con devolución de stock.
- **Insumos, Stock, Compras y Cotizaciones**: Registro de transacciones de stock, conversión de unidades, precisión decimal, multifiltros de kardex, reabastecimiento, reservas automáticas, órdenes de compra, catálogo de proveedores, y cotizaciones con fecha/hora preferida de entrega en zona horaria argentina.
- **Servicios y Paquetes**: Catálogo de servicios, dependencias de permisos, composición de paquetes y cálculo de descuentos.
- **Personal**: Gestión de empleados, roles, filtros y paginación.
- **Auditoría**: Captura granular de eventos de auditoría (catálogo, clientes, vehículos, lavados, sesiones, notificaciones), contratos de escritura y renderizado de vistas parciales de detalle.
- **Seguridad, Roles y Sesiones**: Catálogo y reglas de roles, claims, navegación dinámica, seguridad en restablecimiento de contraseñas, inmutabilidad de roles base y expiración de sesiones.
- **WhatsApp Bot**: Flujos conversacionales de registro y asociación de vehículos, normalización de números telefónicos internacionales/argentinos y controladores de webhooks.
- **Interfaz de Usuario y Presentación**: Consistencia visual, tablas adaptables (responsive), modales multinivel, temas visuales y componentes compartidos.

### 1.3 Herramientas y Entornos Principales

| Entorno / Capa | Framework / Herramienta | Versión Respaldada | Propósito Principal |
|---|---|---|---|
| Backend Testing | [xUnit](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Firebase.Tests.csproj#L19) | 2.9.3 | Ejecución de pruebas unitarias y de integración .NET |
| Backend Mocking | [Moq](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Firebase.Tests.csproj#L18) | 4.20.72 | Dobles de prueba y mocks de interfaces de servicios |
| Backend SDK / Runner | [Microsoft.NET.Test.Sdk](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Firebase.Tests.csproj#L17) | 18.0.1 | Plataforma de ejecución de pruebas VSTest |
| Visual Studio Runner | [xunit.runner.visualstudio](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Firebase.Tests.csproj#L20) | 3.1.5 | Adaptador de ejecución de xUnit |
| Caching en Pruebas | [Microsoft.Extensions.Caching.Memory](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Firebase.Tests.csproj#L16) | 10.0.1 | Caché en memoria durante pruebas de backend |
| Cobertura | [coverlet.collector](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Firebase.Tests.csproj#L15) | 6.0.4 | Recolección de cobertura de código |
| Frontend Testing | Node.js Test Runner (`node:test`) | Nativo Node.js | Pruebas aisladas de scripts JS de cliente |
| DOM Simulation | `node:vm` + Mocks sintéticos | Nativo Node.js | Emulación de objetos globales (`window`, `document`) |
| Browser Testing | [@playwright/test](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/package.json#L18) | 1.64.1 | Automatización de navegador para interfaz de usuario |
| Navegador Local | Chromium | 1248 (local) | Ejecutable ubicado en `.local/playwright-browsers` |
| Emulador Base de Datos | Cloud Firestore Emulator | v1.22.0 jar | Emulación local desechable en puerto 8190 |
| Emulador Autenticación | Firebase Auth Emulator | firebase-tools | Emulación local desechable en puerto 9199 |

### 1.4 Límites de la Evidencia Disponible

- **Sin porcentaje global de cobertura certificado**: El proyecto cuenta con el paquete recolector `coverlet.collector`, pero no existe un reporte consolidado ni mediciones previas de cobertura porcentual por líneas o ramas. Por lo tanto, no se declara ningún porcentaje arbitrario de cobertura.
- **Alcance de las pruebas de navegador**: Con excepción del script específico de notificaciones autenticadas, las 17 suites de Playwright (`tests/browser/*.spec.cjs`) se ejecutan contra un servidor de fixtures estático que sirve HTML pre-renderizado con respuestas HTTP simuladas. No evalúan la compilación dinámica de Razor ni la integración con la base de datos real.
- **Ejecución observada en esta actualización**: Se ejecutaron y verificaron directamente en esta tarea las suites unitarias de JavaScript (221 pruebas en total: 218 aprobadas y 3 fallidas observadas). Las suites de .NET y Playwright no completaron su corrida en este entorno debido a restricciones del entorno de aislamiento (timeout de sockets IPC en VSTest y permisos de sistema de archivos temporales en Playwright), por lo que se documentan a partir del código fuente existente y su configuración explícita.

---

## 2. Organización e Infraestructura de Pruebas

### 2.1 Proyectos y Directorios

El código de pruebas está organizado de la siguiente manera:

```text
Aplicacion-Lavadero/
├── Firebase.Tests/                                # Proyecto principal de pruebas .NET y scripts JS
│   ├── Firebase.Tests.csproj                      # Configuración de dependencias xUnit y .NET 9
│   ├── PasswordResetSecurityTests.cs              # Pruebas de seguridad HTTP en reset de password
│   ├── run-isolated.ps1                           # Script de ejecución con emuladores desechables
│   ├── Helpers/                                   # Utilidades compartidas para pruebas
│   │   ├── TestFactory.cs                         # Factoría de entidades de dominio
│   │   ├── LocalAuthEmulatorTransport.cs          # Transporte de cliente para Auth emulator
│   │   └── firebase-auth-emulator.cjs             # Helper de inicio del emulador de autenticación
│   ├── Services/                                  # 46 archivos .cs de pruebas unitarias de servicios
│   ├── Integration/                               # 61 archivos .cs (60 activos) de integración con emulador
│   ├── Security/                                  # 18 archivos .cs de pruebas de seguridad y RBAC
│   └── *.test.cjs                                 # 44 suites de pruebas JavaScript unitarias (node:test)
├── tests/                                         # Pruebas de integración frontend y navegador
│   ├── lavado-finalizacion.test.js                # Suite unitaria JS de modal de consumos (node:test)
│   └── browser/                                   # 17 archivos .spec.cjs de pruebas Playwright
├── scripts/                                       # Scripts de automatización y verificación
│   ├── playwright.cjs                             # Servidor HTTP de fixture y wrapper de Playwright
│   ├── test-authenticated-notifications.ps1       # Orquestador E2E autenticado con servidor real
│   ├── authenticated-notifications.cjs            # Test Playwright contra servidor real ASP.NET Core
│   └── verify-*-differential.cjs                  # Scripts de verificación diferencial
├── playwright.config.cjs                          # Configuración oficial de Playwright
└── package.json                                   # Definición de dependencias y scripts de prueba npm/pnpm
```

### 2.2 Frameworks y Dependencias Efectivamente Configuradas

#### Backend (.NET 9.0)
El archivo [Firebase.Tests/Firebase.Tests.csproj](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Firebase.Tests.csproj) define:
- **TargetFramework**: `net9.0`
- **xunit** (2.9.3)
- **Moq** (4.20.72)
- **Microsoft.NET.Test.Sdk** (18.0.1)
- **xunit.runner.visualstudio** (3.1.5)
- **Microsoft.Extensions.Caching.Memory** (10.0.1)
- **coverlet.collector** (6.0.4)
- **FrameworkReference**: `Microsoft.AspNetCore.App`
- **ProjectReference**: `..\Firebase.csproj`

#### Frontend (Node.js & Playwright)
El archivo [package.json](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/package.json) define:
- **@playwright/test**: `1.64.1` (con binarios de Chromium ubicados en `.local/playwright-browsers`)
- **Node.js Test Runner**: Módulo nativo `node:test` con `node:assert/strict`.

### 2.3 Mocks, Fixtures y Dobles de Prueba

1. **Fábrica de Datos ([TestFactory.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Helpers/TestFactory.cs))**:
   Proporciona métodos estáticos tipados para instanciar modelos con valores predeterminados válidos:
   - `CreateEmpleado(...)`, `CreateCliente(...)`, `CreateVehiculo(...)`
   - `CreateLavado(...)`, `CreateServicioEnLavado(...)`, `CreateServicio(...)`
   - `CreatePaquete(...)`, `CreateConfiguracion(...)`, `CreateAuditLog(...)`
2. **Dobles de Red y Handlers HTTP**:
   - `FakeHandler` / `CaptureHandler`: Simulan respuestas `HttpResponseMessage` para probar llamadas salientes hacia Firebase REST, Meta WhatsApp Cloud API o el servicio de correo Resend.
   - Servidor Kestrel en memoria: Se utiliza en pruebas de seguridad para instanciar la canalización ASP.NET Core sobre puertos efímeros (`127.0.0.1:0`).
3. **Mocks de Frontend con `node:vm`**:
   - Las pruebas `*.test.cjs` y `tests/lavado-finalizacion.test.js` cargan el archivo de código fuente real de `wwwroot/js/` dentro de un contexto aislado creado con `vm.runInNewContext`. Crean implementaciones simuladas de `window`, `document`, `DOMParser`, `FormData`, `Peticiones` y `Notificaciones`, verificando la lógica de cliente sin dependencias externas de red o navegador.

### 2.4 Emuladores Locales y Condiciones de Aislamiento

El sistema implementa mecanismos estrictos para garantizar que las pruebas nunca interactúen con servicios de producción:

1. **Detección Obligatoria de Emulador (`EmulatorDetection.EmulatorOnly`)**:
   Las pruebas de integración configuran clientes de Firestore mediante `FirestoreDbBuilder` exigiendo `EmulatorDetection.EmulatorOnly`. Si el entorno no apunta a un emulador, la inicialización falla inmediatamente por diseño.
2. **Atributos Personalizados de xUnit**:
   - `[LocalFirestoreFact]`: Atributo derivado de `FactAttribute` que inspecciona la variable de entorno `FIRESTORE_EMULATOR_HOST`. Si no está configurada o no apunta a una dirección `127.0.0.1`, la prueba se omite automáticamente (`Skip`).
   - `[LocalSeedFact]`: Atributo que exige `ISOLATED_FIREBASE_TESTS = "1"`, asegurando que la prueba se ejecute exclusivamente bajo el script orquestador aislado.
3. **Orquestador Aislado ([run-isolated.ps1](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/run-isolated.ps1))**:
   - Inicia Cloud Firestore Emulator v1.22.0 en el puerto `8190`.
   - Inicia Firebase Auth Emulator en el puerto `9199`.
   - Verifica previamente que ambos puertos estén libres; si alguno está ocupado, aborta para evitar reutilizar emuladores existentes o contaminar datos.
   - Ejecuta `dotnet test Firebase.Tests.csproj` contra `demo-suite` y detiene los procesos al finalizar en el bloque `finally`.

---

## 3. Inventario de Pruebas Existentes

A continuación se detalla el inventario completo y clasificado de las pruebas que existen en el repositorio.

### 3.1 Módulo de Notificaciones al Cliente (`ClienteNotificationService`)

Este módulo fue implementado para orquestar la comunicación transaccional directa con los clientes ante cambios de estado en sus lavados, a través de WhatsApp y correo electrónico:

| Componente / Archivo | Tipo de Prueba | Dependencias / Mecanismo | Escenarios y Comportamientos Verificados | Límites Relevantes |
|---|---|---|---|---|
| [ClienteNotificationMessageTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/ClienteNotificationMessageTests.cs) (3 tests) | Unitaria (.NET) | En memoria / Mock HTTP | - `ServiceEvent_UsesFriendlyContextAndTruthfulOutcome`: Mensajes cordiales y veraces en finalización ("finalizó correctamente") y cancelación ("fue cancelado") de servicios, conteniendo el nombre del lavadero, el servicio y marca temporal.<br>- `WhatsAppSequentialAcceptedRequests_DoNotEstablishDelivery`: Verifica que la respuesta HTTP 200 de Meta solo acredita aceptación por el proveedor, no entrega al destinatario.<br>- `CompletedStage_ReportsNextStageRatherThanCurrentService`: Al completarse una etapa operativa, el mensaje informa la próxima etapa (`Sigue: Enjuague.`) en lugar de reiterar el nombre del servicio completo. | No realiza envíos de red reales a Meta ni a servidores de correo. |
| [ClienteNotificationPreferenceTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/ClienteNotificationPreferenceTests.cs) (4 tests) | Unitaria (.NET) | En memoria | - `LegacyClient_UsesEmailAsEffectiveDefault`: Normalización para clientes existentes sin preferencias explícitas (adopta email activo y WhatsApp inactivo por defecto).<br>- `ExplicitPreferences_ArePreserved`: Preservación estricta de elecciones explícitas (WhatsApp solo, Email solo o ambos).<br>- `ExplicitlyEmptyPreferences_AreRejected`: Lanza `ValidationException` si se intentan desactivar ambos canales simultáneamente.<br>- `CancellationMessage_ContainsReasonAndDoesNotClaimCompletion`: La notificación de cancelación de lavado incluye el motivo explicativo y nunca afirma que el vehículo esté listo para retirar. | Validación de lógica en memoria. |
| [MetaWhatsAppServiceTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/MetaWhatsAppServiceTests.cs) (6 tests: 1 Fact + 5 Theories) | Unitaria (.NET) | Moq / `ThrowingHandler` | - Formateo de payloads para texto, botones y listas interactivas.<br>- `TransportDiagnostics_AreStructuredAndNeverLeak`: Ante fallas de transporte HTTP o cancelaciones, genera diagnóstico clasificado como `"uncertain transport"` y registra logs de advertencia estructurados **sin filtrar secretos**, tokens, números de teléfono o URLs privadas.<br>- `TransportFailure_IsUncertainAndSafe`: Clasificación segura de excepciones de red. | Pruebas de contrato de cliente HTTP. |
| [LavadoCreationPricingRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/LavadoCreationPricingRefactorTests.cs) (3 tests específicos de notificaciones) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | - `ClienteNotification_SequentialStagesUseConfiguredNameAndContinueAfterNotificationAuditFailure`: Despacho dual (Email y WhatsApp) ante etapas consecutivas con el nombre configurado del lavadero, transición hacia el mensaje de retiro en la última etapa, y continuidad operativa si el registro de auditoría de la notificación falla (`NotificationAuditFailure`).<br>- `ClienteNotification_RepeatedStageCompletionDoesNotResend`: Idempotencia; reintentar finalizar una etapa ya completada no duplica mensajes de notificación.<br>- `Horario_CreationOutsideHoursReturnsSpecificErrorWithoutWrites`: Rechazo específico ante solicitudes fuera de horario de atención comercial sin escrituras espurias en la base de datos. | Requiere emulador en puerto 8190. |

### 3.2 Clientes

| Componente / Archivo | Tipo de Prueba | Dependencias / Mecanismo | Escenarios y Comportamientos Verificados | Límites Relevantes |
|---|---|---|---|---|
| [ClienteServiceTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/ClienteServiceTests.cs) (21 tests) | Unitaria (.NET) | En memoria / Sin BD | Propiedades del modelo `Cliente`, concatenación de `NombreCompleto`, regex de nombres (acentos, espacios, mín. 3 letras), solo números en DNI, teléfono de 10 dígitos, formato de email, filtros por estado múltiple, búsqueda insensible a mayúsculas en múltiples campos, ordenamiento alfabético, cálculo de páginas y detección de documentos duplicados. | No prueba persistencia en Firestore ni transacciones concurrentes. |
| [ClienteQueryRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ClienteQueryRefactorTests.cs) (2 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Listado y conteo de clientes respetando combinaciones de filtros, orden ascendente/descendente por nombre y paginación exacta. | Requiere emulador en puerto 8190. |
| [ClienteAssociationRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ClienteAssociationRefactorTests.cs) (3 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Asociación y desvinculación de clientes a vehículos, conservación de lista de vehículos asociados y consistencia referencial bidireccional. | Requiere emulador en puerto 8190. |
| [ClienteLifecycleRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ClienteLifecycleRefactorTests.cs) (1 test) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Ciclo de vida completo del cliente: alta, actualización de datos (incluidas preferencias de notificación), baja lógica (inactivación) y persistencia del estado. | Requiere emulador en puerto 8190. |
| [ClienteVehicleCatalogueProposalTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ClienteVehicleCatalogueProposalTests.cs) (5 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Creación de cliente vinculada a propuestas de nuevos modelos de vehículos en el catálogo cuando no existen previamente. | Requiere emulador en puerto 8190. |
| [ClienteSecurityRegressionTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Security/ClienteSecurityRegressionTests.cs) (1 test) | Seguridad (.NET) | Simulación de claims | Verificación de permisos de usuario para modificar clientes y aislamiento de datos. | Sin conexión HTTP real. |
| [ClienteLayout.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/ClienteLayout.test.cjs) (2 tests) | Frontend JS | Node.js `node:test` + `node:vm` | Renderizado adaptativo de formularios de cliente y grilla de vehículos asociados. | Ejecución en DOM sintético. |

### 3.3 Vehículos, Titularidad y Catálogo

| Componente / Archivo | Tipo de Prueba | Dependencias / Mecanismo | Escenarios y Comportamientos Verificados | Límites Relevantes |
|---|---|---|---|---|
| [VehiculoServiceTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/VehiculoServiceTests.cs) (11 tests) | Unitaria (.NET) | En memoria | Formato de clave de asociación `XXXX-XXXX` (9 caracteres), exclusión de caracteres ambiguos (O, 0, 1, I, L), consistencia de hash SHA-256, normalización (remueve guiones, mayúsculas) y validación de claves correctas/incorrectas. | Solo prueba la lógica criptográfica y de formato de claves. |
| [CarQueryVehicleTypeTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/CarQueryVehicleTypeTests.cs) (3 tests) | Unitaria (.NET) | En memoria | Mapeo y detección de categorías de vehículos (Sedán, SUV, Camioneta) a partir de respuestas de API externa de catálogo. | Respuestas simuladas. |
| [HybridVehicleCatalogueTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/HybridVehicleCatalogueTests.cs) (7 tests) | Unitaria (.NET) | En memoria | Catálogo híbrido de vehículos: búsqueda local combinada con catálogo en línea, resolución de marcas y modelos. | Sin red externa. |
| [VehicleCatalogueProposalTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/VehicleCatalogueProposalTests.cs) (5 tests) | Unitaria (.NET) | En memoria | Creación, estados y validación de propuestas de vehículos introducidas por clientes o recepcionistas. | En memoria. |
| [VehicleInputResolverTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/VehicleInputResolverTests.cs) (5 tests) | Unitaria (.NET) | En memoria | Resolución de términos de entrada de vehículos, normalización de marcas y modelos ingresados libremente. | En memoria. |
| [VehicleNameMatcherTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/VehicleNameMatcherTests.cs) (3 tests) | Unitaria (.NET) | En memoria | Algoritmo de coincidencia difusa de nombres de vehículos para evitar duplicaciones en el catálogo. | En memoria. |
| [VehicleOwnershipRegressionTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/VehicleOwnershipRegressionTests.cs) (19 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Regresiones críticas de titularidad: rechazo de claves de asociación inválidas sin escrituras huérfanas, reasignación de vehículos huérfanos sin titular, bloqueo de desvinculación de titular compartido, guardias transaccionales de patente (`vehiclePlateGuards`) y resolución de nombres actuales de titulares vía lecturas agrupadas. | Requiere emulador en puerto 8190. |
| [VehicleCatalogueFirestoreTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/VehicleCatalogueFirestoreTests.cs) (7 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Persistencia y consulta de marcas y modelos en Firestore, transiciones de estado de propuestas aprobadas/rechazadas. | Requiere emulador en puerto 8190. |
| [VehicleCatalogueReviewHttpSecurityTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/VehicleCatalogueReviewHttpSecurityTests.cs) (2 tests) | Integración (.NET) | Kestrel local / Firestore | Seguridad HTTP y permisos requeridos para revisar y autorizar propuestas de catálogo. | Requiere emulador. |
| [VehiculoQueryRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/VehiculoQueryRefactorTests.cs) (2 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Consultas de vehículos con filtros por patente, marca, cliente y estado. | Requiere emulador. |
| [VehiculoMutationRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/VehiculoMutationRefactorTests.cs) (2 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Operaciones de mutación, actualización de campos y validación de unicidad de patente. | Requiere emulador. |
| [VehiculoAvailabilityRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/VehiculoAvailabilityRefactorTests.cs) (1 test) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Disponibilidad y asignación de vehículos para nuevos servicios. | Requiere emulador. |
| [WhatsAppVehicleFirestoreTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/WhatsAppVehicleFirestoreTests.cs) (16 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Flujos de vehículos registrados o asociados a través del canal de WhatsApp: registro atómico de cliente y vehículo, persistencia de propuestas y emisión de claves. | Requiere emulador. |
| [VehicleCatalogueReviewSecurityTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Security/VehicleCatalogueReviewSecurityTests.cs) (2 tests) | Seguridad (.NET) | En memoria | Restricción de acceso para la revisión de catálogo a roles administrativos. | Sin conexión HTTP. |
| [VehicleSearchSort.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/VehicleSearchSort.test.cjs) (2 tests) | Frontend JS | Node.js `node:test` + `node:vm` | Búsqueda y ordenamiento en cliente de la grilla de vehículos. | DOM sintético. |
| [vehicle-ownership.spec.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/tests/browser/vehicle-ownership.spec.cjs) (7 tests) | Navegador (Playwright) | Fixture local (puerto 41783) | Advertencia visual al quitar titular compartido, ingreso y envío de clave de asociación, modal de personas asociadas y feedback de error. | Respuestas HTTP simuladas. |

### 3.4 Lavados, Recetas y Etapas Operativas

| Componente / Archivo | Tipo de Prueba | Dependencias / Mecanismo | Escenarios y Comportamientos Verificados | Límites Relevantes |
|---|---|---|---|---|
| [LavadoServiceTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/LavadoServiceTests.cs) (46 tests) | Unitaria (.NET) | En memoria | Estados válidos del lavado (`Pendiente`, `EnProceso`, `Realizado`, `RealizadoParcialmente`, `Cancelado`), estado inicial, retiro (`Pendiente`, `Retirado`), cálculo de precios como suma de servicios menos descuentos (rango 0-100%), suma de tiempos estimados, estados de pago (`Pendiente`, `Parcial`, `Pagado`), medios de pago, validación de al menos un servicio, asignación de empleados (rango 1 a 10) y validación de motivo obligatorio en cancelaciones. | No valida persistencia transaccional. |
| [LavadoRecetaTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/LavadoRecetaTests.cs) (2 tests) | Unitaria (.NET) | En memoria | Consumo teórico de recetas de insumos asociadas a los servicios configurados en el lavado. | En memoria. |
| [LavadoQueryRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/LavadoQueryRefactorTests.cs) (1 test) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Consultas de lavados filtrando por rangos de fecha, clientes, patentes y estados operativos. | Requiere emulador. |
| [LavadoCreationPricingRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/LavadoCreationPricingRefactorTests.cs) (14 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Creación transaccional de lavados, fijación de precios según tipo de vehículo, verificación de notificaciones secuenciales a clientes, idempotencia de finalización de etapas y control de horarios comerciales. | Requiere emulador. |
| [LavadoCancelacionConsumosIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/LavadoCancelacionConsumosIntegrationTests.cs) (9 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Cancelación de lavados o etapas con consumo real registrado: registro de insumos devueltos vs. consumidos efectivamente. | Requiere emulador. |
| [LavadoAssociatedClientRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/LavadoAssociatedClientRefactorTests.cs) (2 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Creación de lavados solicitados por clientes asociados al vehículo (no solo el titular). | Requiere emulador. |
| [LavadoInsumosTiposIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/LavadoInsumosTiposIntegrationTests.cs) (15 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Consumo de insumos por tipo y categoría durante las etapas operativas del lavado. | Requiere emulador. |
| [LavadoInsumosHttpWorkflowIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/LavadoInsumosHttpWorkflowIntegrationTests.cs) (4 tests) | Integración (.NET) | Kestrel local / Firestore | Flujo HTTP de inicio y finalización de servicios en lavados con ajuste de stock. | Requiere emulador. |
| [CreateOnlyWashRoleTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Security/CreateOnlyWashRoleTests.cs) (7 tests) | Seguridad (.NET) | En memoria | Restricción de roles con permisos de solo creación de lavados (no pueden finalizar ni cancelar sin permisos específicos). | En memoria. |
| [tests/lavado-finalizacion.test.js](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/tests/lavado-finalizacion.test.js) (21 tests) | Frontend JS | Node.js `node:test` + `node:vm` | Validación en cliente del modal de consumos reales: restauración de motivo sin POST ante `Escape`/`Cancel`, envío de consumos adicionales en unidad propia, rechazo de valores negativos y `NaN`, justificación obligatoria ante desvíos de consumos y preservación de rutas y parámetros de finalización. | DOM simulado. |
| [WashDetailNotifications.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/WashDetailNotifications.test.cjs) (1 test) | Frontend JS | Node.js `node:test` + Playwright Chromium | Notificaciones en vista de detalle de lavado y envío de tokens antiforgery. | Test ejecutado con fallo observado en mensaje esperado. |
| [WashModalLayering.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/WashModalLayering.test.cjs) (5 tests) | Frontend JS | Node.js `node:test` + `node:vm` | Superposición de modales en el detalle de lavado, persistencia de backdrop y cierre con tecla Escape. | DOM sintético. |
| [LavadoInsumosModalConsistency.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/LavadoInsumosModalConsistency.test.cjs) (3 tests) | Frontend JS | Node.js `node:test` + `node:vm` | Consistencia de modales de insumos en el flujo operativo de lavados. | DOM sintético. |
| [wash-modal-layering.spec.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/tests/browser/wash-modal-layering.spec.cjs) (1 test) | Navegador (Playwright) | Fixture local (puerto 41783) | Capas de z-index y bloqueo de scroll al abrir modales en la pantalla de lavados. | Servidor fixture estático. |

### 3.5 Insumos, Stock, Kardex, Compras, Proveedores, Órdenes y Cotizaciones

| Componente / Archivo | Tipo de Prueba | Dependencias / Mecanismo | Escenarios y Comportamientos Verificados | Límites Relevantes |
|---|---|---|---|---|
| [InsumosTransaccionesTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/InsumosTransaccionesTests.cs) (3 tests) | Unitaria (.NET) | En memoria | Registro de movimientos de entrada, salida y ajustes en el inventario. | Sin persistencia. |
| [InsumosOperacionPermisosTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/InsumosOperacionPermisosTests.cs) (1 test) | Unitaria (.NET) | En memoria | Matriz de permisos requerida para operar movimientos de stock. | En memoria. |
| [InventarioAgrupadoTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/InventarioAgrupadoTests.cs) (12 tests) | Unitaria (.NET) | En memoria | Agrupación de stock por categoría de insumo, cálculo de totales y stock disponible vs. reservado. | En memoria. |
| [StockPrecisionTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/StockPrecisionTests.cs) (4 tests) | Unitaria (.NET) | En memoria | Precisión decimal en cantidades fraccionarias de insumos líquidos o dosificados. | En memoria. |
| [TipoInsumoTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/TipoInsumoTests.cs) (4 tests) | Unitaria (.NET) | En memoria | Clasificación de insumos por categorías y tipos. | En memoria. |
| [ConversionUnidadesTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/ConversionUnidadesTests.cs) (18 tests) | Unitaria (.NET) | En memoria | Conversión entre unidades de medida (litros, mililitros, unidades, kilogramos, gramos) y validaciones de equivalencia. | En memoria. |
| [RecetaUnidadTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/RecetaUnidadTests.cs) (2 tests) | Unitaria (.NET) | En memoria | Unidades de medida configuradas en recetas de servicios y compatibilidad con unidades de stock. | En memoria. |
| [PreferredDeliveryIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/PreferredDeliveryIntegrationTests.cs) (5 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Solicitudes de cotización con fecha y hora preferidas de entrega: validación en zona horaria argentina (`America/Argentina/Buenos_Aires`), persistencia UTC, rechazo de fechas pasadas y campos incompletos sin escrituras ni auditoría, y formateo en resúmenes. | Requiere emulador. |
| [InsumosStockIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/InsumosStockIntegrationTests.cs) (48 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Suite integral de stock: actualización atómica de existencias, descuentos por lavados, alertas de stock mínimo, bloqueo de stock negativo y transacciones concurrentes. | Requiere emulador. |
| [InsumosTiposIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/InsumosTiposIntegrationTests.cs) (19 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Altas, bajas, modificaciones y consultas de tipos de insumo en Firestore. | Requiere emulador. |
| [InsumoCatalogIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/InsumoCatalogIntegrationTests.cs) (9 tests) & [InsumosCatalogIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/InsumosCatalogIntegrationTests.cs) (9 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Catálogo maestro de insumos, validación de nombres únicos, precios de compra y proveedores predeterminados. | Requiere emulador. |
| [InventarioReservasIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/InventarioReservasIntegrationTests.cs) (3 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Reservas de insumos al programar servicios y liberación o consumo al cancelar o finalizar el lavado. | Requiere emulador. |
| [KardexServiceIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/KardexServiceIntegrationTests.cs) (3 tests), [KardexHttpIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/KardexHttpIntegrationTests.cs) (9 tests), [KardexMultifiltroIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/KardexMultifiltroIntegrationTests.cs) (1 test) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Registro inmutable de movimientos en Kardex, trazabilidad por usuario/servicio, filtros combinados por fecha, insumo y tipo de movimiento, y paginación. | Requiere emulador. |
| [ComprasWorkflowIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ComprasWorkflowIntegrationTests.cs) (36 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Flujo completo de compras: creación de cotizaciones, conversión a órdenes de compra, validación de montos, recepción parcial o total de insumos e impacto en stock. | Requiere emulador. |
| [OrdenesHttpIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/OrdenesHttpIntegrationTests.cs) (8 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Controladores HTTP para gestión de órdenes de compra, permisos y transiciones de estado. | Requiere emulador. |
| [ProveedorCatalogIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ProveedorCatalogIntegrationTests.cs) (14 tests), [ProveedorEmailLockIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ProveedorEmailLockIntegrationTests.cs) (5 tests), [ProveedorValidationHttpIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ProveedorValidationHttpIntegrationTests.cs) (1 test) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Gestión de proveedores: unicidad de CUIT/RUT, bloqueo de modificación de email durante órdenes activas y validaciones HTTP. | Requiere emulador. |
| [ReabastecimientoIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ReabastecimientoIntegrationTests.cs) (17 tests) & [RecepcionParcialCierreIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/RecepcionParcialCierreIntegrationTests.cs) (4 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Algoritmos de sugerencia de reabastecimiento por stock crítico, recepciones parciales con cierre definitivo o pendiente. | Requiere emulador. |
| [InsumosEmailFormattingIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/InsumosEmailFormattingIntegrationTests.cs) (7 tests) | Integración (.NET) | En memoria / Emulador | Generación y formateo de correos electrónicos de órdenes, cotizaciones con fecha/hora preferida y alertas de stock. | Sin envío de correo real. |
| [PreferredDeliveryUi.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/PreferredDeliveryUi.test.cjs) (3 tests) | Frontend JS | Node.js `node:test` + `node:vm` | Validación de fecha y hora preferida de entrega opcionales en formulario de cotizaciones, mínimos dinámicos según hora local argentina y actualización de ayuda contextual. | DOM sintético. |
| 17 Suites JavaScript de Insumos (`*.test.cjs`) | Frontend JS | Node.js `node:test` + `node:vm` | Verifican consistencia visual de tarjetas y tablas de insumos, estilos de paginación, filtros multifiltro de Kardex, detalle de stock y órdenes, reactivación de estados y templates de email. | DOM sintético. |

### 3.6 Servicios y Paquetes

| Componente / Archivo | Tipo de Prueba | Dependencias / Mecanismo | Escenarios y Comportamientos Verificados | Límites Relevantes |
|---|---|---|---|---|
| [ServicioServiceTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/ServicioServiceTests.cs) (19 tests) | Unitaria (.NET) | En memoria | Modelo de servicio, precios por tipo de vehículo, tiempo estimado, estados y búsquedas insensibles a mayúsculas. | En memoria. |
| [PaqueteServicioServiceTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/PaqueteServicioServiceTests.cs) (28 tests) | Unitaria (.NET) | En memoria | Composición de paquetes con múltiples servicios, cálculo de porcentaje de descuento, tipos de vehículo admitidos y validaciones. | En memoria. |
| [PaquetePermissionDependenciesTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/PaquetePermissionDependenciesTests.cs) (1 test) | Unitaria (.NET) | En memoria | Dependencias de permisos entre administración de servicios y paquetes. | En memoria. |
| [ServicioQueryRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ServicioQueryRefactorTests.cs) (2 tests) & [ServicioMutationRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ServicioMutationRefactorTests.cs) (3 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Consultas de servicios activos y mutaciones de catálogo en base de datos. | Requiere emulador. |
| [ServicioRecetaIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ServicioRecetaIntegrationTests.cs) (9 tests) & [ServicioRecetaRenderIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ServicioRecetaRenderIntegrationTests.cs) (2 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Persistencia y renderizado de recetas de insumos asociadas a cada servicio. | Requiere emulador. |
| [PaqueteQueryRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/PaqueteQueryRefactorTests.cs) (1 test) & [PaqueteMutationRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/PaqueteMutationRefactorTests.cs) (1 test) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Consultas y mutaciones de paquetes de servicios en Firestore. | Requiere emulador. |
| [ServicioRecetaModalConsistency.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/ServicioRecetaModalConsistency.test.cjs) & [ServicioRecipeDetails.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/ServicioRecipeDetails.test.cjs) | Frontend JS | Node.js `node:test` + `node:vm` | Modales y formularios de edición de recetas de insumos en la interfaz de servicios. | DOM sintético. |

### 3.7 Personal

| Componente / Archivo | Tipo de Prueba | Dependencias / Mecanismo | Escenarios y Comportamientos Verificados | Límites Relevantes |
|---|---|---|---|---|
| [PersonalServiceTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/PersonalServiceTests.cs) (11 tests) | Unitaria (.NET) | En memoria | Modelo de empleado, validación de email, asignación de roles iniciales, estados activo/inactivo y paginación. | En memoria. |
| [PersonalQueryRefactorTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/PersonalQueryRefactorTests.cs) (4 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Consultas de personal con filtros por rol, estado y búsqueda por nombre en base de datos. | Requiere emulador. |

### 3.8 Auditoría

| Componente / Archivo | Tipo de Prueba | Dependencias / Mecanismo | Escenarios y Comportamientos Verificados | Límites Relevantes |
|---|---|---|---|---|
| 10 Suites Unitarias de Auditoría ([AuditServiceTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/AuditServiceTests.cs), [AuditChangeDetailsTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/AuditChangeDetailsTests.cs), etc.) (71 tests en total) | Unitaria (.NET) | En memoria | Formateo de cambios en campos (`AuditChangeDetailsTests` cuenta con 42 tests unitarios exhaustivos), captura de sesión, captura de eventos de catálogo, lavados y notificaciones, contratos del escritor de auditoría y renderizado de vistas parciales. | En memoria. |
| 6 Suites de Integración de Auditoría ([AuditCaptureTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/AuditCaptureTests.cs), etc.) (13 tests en total) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Inmutabilidad de registros en la colección `auditoria`, consultas por rango de fechas, usuario y entidad afectada. | Requiere emulador. |
| [AuditDateRange.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/AuditDateRange.test.cjs) & [AuditDetail.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/AuditDetail.test.cjs) | Frontend JS | Node.js `node:test` + `node:vm` | Validación de fechas invertidas (rechaza recarga sin petición al servidor) y modal de visualización de detalles de auditoría. | DOM sintético. |
| [auditoria.spec.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/tests/browser/auditoria.spec.cjs) (8 tests) | Navegador (Playwright) | Fixture local (puerto 41783) | Modal de detalle de auditoría: etiquetas en negrita con valores en línea, fondo oscurecido, cierre con X y Escape, foco del teclado y estilos de tabla. | Respuestas simuladas. |

### 3.9 Autenticación, Seguridad y Roles (RBAC)

| Componente / Archivo | Tipo de Prueba | Dependencias / Mecanismo | Escenarios y Comportamientos Verificados | Límites Relevantes |
|---|---|---|---|---|
| [AuthenticationServiceTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/AuthenticationServiceTests.cs) (21 tests) | Unitaria (.NET) | Moq / En memoria | Validación de credenciales de usuario, login por email y contraseña, mapeo de roles y claims de identidad. | Dobles de prueba de Firebase Auth. |
| [PasswordResetSecurityTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/PasswordResetSecurityTests.cs) (6 tests) | Seguridad (.NET) | Kestrel local efímero (`127.0.0.1:0`) | Verificación estricta de seguridad en recuperación de contraseña: rechazo de entradas inválidas sin invocar proveedores externos (mitigación de costos y denegación de servicio), respuesta idéntica y genérica ante emails existentes e inexistentes para evitar enumeración de cuentas. | Kestrel en memoria sin salida externa. |
| [ResendEmailSenderTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/ResendEmailSenderTests.cs) (3 tests) | Unitaria (.NET) | Moq / FakeHandler | Contrato de envío de correos mediante API de Resend, manejo de errores de red y tokens. | No envía correos reales. |
| [RolCatalogoTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/RolCatalogoTests.cs) (10 tests), [RolDependenciasTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/RolDependenciasTests.cs) (7 tests), [RolModelTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/RolModelTests.cs) (1 test), [RolReglasTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/RolReglasTests.cs) (6 tests) | Unitaria (.NET) | En memoria | Catálogo y modelo de roles, jerarquías de dependencias entre permisos, inmutabilidad de roles del sistema y validaciones. | En memoria. |
| 18 Suites en [Firebase.Tests/Security/](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Security) (78 tests) | Seguridad (.NET) | En memoria / Claims / Kestrel | Pruebas de matriz de permisos RBAC (`RbacSpecProbes`), regeneración de menú de navegación según permisos (`NavegacionSistemaTests`), roles con solo permisos de creación en catálogo y lavados, login inicial con Google (`GoogleFirstLoginTests`), siembra de credenciales de emulador local, expiración de sesiones y privacidad en endpoints de actividad. | Sin conexión a servicios productivos. |
| [LocalEmulatorLoginTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/LocalEmulatorLoginTests.cs) (1 test) | Integración (.NET) | Auth Emulator (`[LocalSeedFact]`) | Autenticación real contra el emulador local de Firebase Auth, validación de Uid y rechazo con credenciales incorrectas. | Requiere Auth emulator en puerto 9199. |
| [RolInsumosBaseIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/RolInsumosBaseIntegrationTests.cs) (6 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Persistencia y asignación de permisos sobre módulos de insumos. | Requiere emulador. |
| 6 Suites JavaScript de Seguridad y Autenticación (`*.test.cjs`) | Frontend JS | Node.js `node:test` + `node:vm` | Validación de login en cliente, manejo de errores de Google Login, interfaz de reseteo de contraseña, refresco de navegación por roles y cierre de sesión. | DOM sintético. |
| [login-validation.spec.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/tests/browser/login-validation.spec.cjs) (1 test) | Navegador (Playwright) | Fixture local (puerto 41783) | Validación visual de campos requeridos y formato de correo en la pantalla de acceso. | Respuestas simuladas. |
| [test-authenticated-notifications.ps1](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/scripts/test-authenticated-notifications.ps1) & [authenticated-notifications.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/scripts/authenticated-notifications.cjs) | E2E Autenticado (Playwright + ASP.NET Core) | Servidor real (puerto 5200) + Emuladores (8189 y 9099) | Flujo E2E completo: inicio de sesión de administrador, apertura de formulario de rol, remoción de permiso de configuración, guardado AJAX, verificación de regeneración del menú lateral (desaparición de enlace), consumo único de mensaje `TempData`, auto-revocación y comprobación de respuesta HTTP 403 (Acceso Denegado). | Requiere entorno con puertos libres y dependencias instaladas. |

### 3.10 WhatsApp Bot y Telefonía

| Componente / Archivo | Tipo de Prueba | Dependencias / Mecanismo | Escenarios y Comportamientos Verificados | Límites Relevantes |
|---|---|---|---|---|
| [WhatsAppFlowServiceTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/WhatsAppFlowServiceTests.cs) (26 tests) | Unitaria (.NET) | En memoria | Máquina de estados finitos del flujo conversacional: estados de inicio, registro, vehículos, menú de cliente, edición de perfil (con preferencias de notificación) y asociación. Reconocimiento de comandos especiales (`INICIO`, `MENU`, `REINICIAR`), persistencia en `TemporaryData` y actualización de fecha de última interacción. | En memoria. |
| [PhoneNumberHelperTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/PhoneNumberHelperTests.cs) (21 tests) | Unitaria (.NET) | En memoria | Normalización de números telefónicos: remoción de caracteres no numéricos, manejo del prefijo internacional `+54` y el prefijo móvil `9` característico de Argentina, preparación para API de Meta y comparación flexible de números. | En memoria. |
| [WhatsAppOperationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/WhatsAppOperationTests.cs) (4 tests) & [WhatsAppWebhookControllerTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/WhatsAppWebhookControllerTests.cs) (10 tests) | Unitaria (.NET) | Controller / Moq | Verificación del webhook de Meta (validación de token de suscripción de webhook mediante desafío `hub.challenge`), recepción y procesamiento asíncrono de mensajes entrantes. | En memoria. |
| [WhatsAppAssociationKeyTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/WhatsAppAssociationKeyTests.cs) (9 tests: 2 Unitarias + 7 Integración) | Híbrida (.NET / Firestore) | Firestore Emulator (`[LocalFirestoreFact]`) | Generación y entrega de claves de asociación vehicular a través de mensajes de WhatsApp, validación contra base de datos. | Requiere emulador. |

### 3.11 Configuración del Sistema y Componentes UI Transversales

| Componente / Archivo | Tipo de Prueba | Dependencias / Mecanismo | Escenarios y Comportamientos Verificados | Límites Relevantes |
|---|---|---|---|---|
| [ConfiguracionServiceTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/ConfiguracionServiceTests.cs) (19 tests) | Unitaria (.NET) | En memoria | Parámetros de configuración general: nombre del lavadero utilizado en notificaciones, porcentajes de descuento por cancelación, horarios comerciales y capacidades. | En memoria. |
| [TableEmptyStateRenderingTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Services/TableEmptyStateRenderingTests.cs) (2 tests) | Unitaria (.NET) | En memoria | Renderizado de estados vacíos en tablas cuando no hay registros disponibles. | En memoria. |
| [ConfiguracionStockIntegrationTests.cs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/Integration/ConfiguracionStockIntegrationTests.cs) (4 tests) | Integración (.NET) | Firestore Emulator (`[LocalFirestoreFact]`) | Parámetros de configuración de inventario y alertas en base de datos. | Requiere emulador. |
| 13 Suites JavaScript Transversales (`*.test.cjs`) | Frontend JS | Node.js `node:test` + `node:vm` | Botones de formulario (`FormButtons.test.cjs`), contraste de colores (`FormButtonContrastRegression.test.cjs`), inicialización de validación dinámica (`DynamicFormValidation.test.cjs`), controles de tabla (`TableControlsConsistency.test.cjs`), tema claro/oscuro (`Theme.test.cjs`), e integración del helper de peticiones (`NotificationIntegration.test.cjs`). | DOM sintético. |
| 13 Suites Playwright Transversales (`tests/browser/*.spec.cjs`) | Navegador (Playwright) | Fixture local (puerto 41783) | Validación en navegador de botones, formularios dinámicos, notificaciones (apilamiento, auto-cierre, descarte), menú lateral móvil (sidebar overlay), controles de tabla y diseño adaptativo. | Respuestas simuladas. |

---

## 4. Pruebas de Navegador

### 4.1 Arquitectura y Modo de Operación

Las pruebas de navegador implementadas en `tests/browser/*.spec.cjs` utilizan **Playwright** con el navegador **Chromium** en modo desatendido (*headless*).

Su arquitectura de ejecución se describe a continuación:
- **Punto de Entrada**: La configuración definida en [playwright.config.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/playwright.config.cjs) especifica como directorio de pruebas `tests/browser`, con un solo worker (`workers: 1`) y cero reintentos (`retries: 0`).
- **Servidor Fixture Local**: Al iniciar las pruebas, Playwright arranca un servidor HTTP propio ejecutando `node scripts/playwright.cjs serve` en la dirección fija `http://127.0.0.1:41783`. Este servidor rechaza puertos ocupados y no reutiliza instancias previas.
- **Carga de Componentes Reales**: El servidor entrega un documento HTML estático que incrusta el contenido real de vistas parciales (como `Views/Auditoria/_AuditoriaDetalle.cshtml`) junto con las hojas de estilo y los scripts cliente reales de `wwwroot/js/`.
- **Aislamiento de Red**: En las pruebas de navegador estándar, las solicitudes hacia endpoints externos se bloquean. Las llamadas hacia acciones de controladores (como `/Auditoria/TablePartial` o `/Auditoria/SearchPartial`) son interceptadas o respondidas por el servidor fixture con fragmentos HTML sintéticos mínimos.

### 4.2 Alcance Real vs. Límites de las Pruebas de Navegador

> [!IMPORTANT]
> **Lo que SÍ verifican las pruebas de navegador estándar**:
> - La correcta carga e inicialización de scripts de interfaz en un motor de navegador Chromium real.
> - El comportamiento del DOM ante eventos de usuario (clics, teclas como Escape, envío de formularios).
> - La accesibilidad por teclado y la navegación de foco dentro de modales.
> - La visibilidad, superposición de capas (*z-index*), transiciones y clases de Tailwind CSS en modales y menús laterales.
> - El cambio inmediato y la persistencia de clases de tema oscuro/claro ante preferencias del sistema (`matchMedia`) o selección manual.
> - La presentación visual de mensajes de error de validación en tiempo real.

> [!WARNING]
> **Lo que NO verifican las pruebas de navegador estándar**:
> - **No ejecutan el servidor ASP.NET Core MVC**: No prueban enrutamiento de controladores, filtros de acción ni model binding de C#.
> - **No compilan vistas Razor dinámicas**: Solo cargan fragmentos de Razor que fueron leídos como texto estático o incrustados en el fixture.
> - **No acceden a Firebase Firestore ni Firebase Auth**: Todos los datos mostrados son respuestas simuladas o datos de prueba inyectados.
> - **No verifican autenticación de sesión real**: La sesión de usuario y las cookies de autenticación no están presentes.

### 4.3 La Excepción: Prueba E2E Autenticada

El archivo [scripts/test-authenticated-notifications.ps1](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/scripts/test-authenticated-notifications.ps1) ejecuta la única prueba end-to-end real que rompe este límite:
1. Inicia emuladores locales desechables de Firestore (puerto 8189) y Auth (puerto 9099).
2. Siembra datos de prueba mediante `dotnet run --project Firebase.csproj -- --seed-local-emulators`.
3. Levanta la aplicación real ASP.NET Core mediante `dotnet run` en `http://127.0.0.1:5200`.
4. Ejecuta [scripts/authenticated-notifications.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/scripts/authenticated-notifications.cjs) con Chromium para iniciar sesión como administrador real, mutar permisos en vivo y comprobar respuestas HTTP 403 genuinas del servidor web.

---

## 5. Ejecución y Requisitos

A continuación se detallan los comandos reales y verificables para ejecutar cada suite de pruebas del proyecto.

### 5.1 Pruebas .NET (Unitarias y de Servicios)

Para ejecutar pruebas unitarias de backend que no requieren emuladores de base de datos:

- **Directorio de ejecución**: Raíz del repositorio o `Firebase.Tests/`.
- **Requisitos**: .NET 9.0 SDK instalado.

```powershell
# Ejecutar todas las pruebas unitarias de servicios y seguridad (sin emuladores)
dotnet test Firebase.Tests/Firebase.Tests.csproj --filter "FullyQualifiedName~Services|FullyQualifiedName~Security"

# Ejecutar las pruebas del módulo de notificaciones al cliente
dotnet test Firebase.Tests/Firebase.Tests.csproj --filter "FullyQualifiedName~ClienteNotification"

# Ejecutar con salida detallada
dotnet test Firebase.Tests/Firebase.Tests.csproj --logger "console;verbosity=detailed"
```

### 5.2 Pruebas .NET de Integración Aislada (con Emuladores)

Para ejecutar las pruebas que interactúan con Firestore y Firebase Auth (`[LocalFirestoreFact]` y `[LocalSeedFact]`):

- **Directorio de ejecución**: Raíz del repositorio.
- **Requisitos**:
  - OpenJDK 21 (provisto en `.local/firebase-emulator/jdk/jdk-21.0.13.0.2+1/bin/java.exe`).
  - Jar del emulador de Firestore (provisto en `.local/firebase-emulator/home/.cache/firebase/emulators/`).
  - Node.js instalado.
  - Puertos locales `8190` y `9199` completamente desocupados.
- **Advertencia**: No ejecutar contra servicios de producción. El script configura automáticamente variables de entorno para garantizar el aislamiento.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "& ./Firebase.Tests/run-isolated.ps1"
```

### 5.3 Pruebas JavaScript Unitarias y de Componentes (`node:test`)

Para ejecutar las 45 suites de pruebas de frontend en entorno DOM simulado:

- **Directorio de ejecución**: Raíz del repositorio.
- **Requisitos**: Node.js instalado. No requiere navegador ni servidor web.

```powershell
# Ejecutar todas las suites .test.cjs ubicadas en Firebase.Tests
node --test (Get-ChildItem -Path "Firebase.Tests/*.test.cjs" | ForEach-Object { $_.FullName })

# Ejecutar la suite de finalización y consumos de lavado
node --test tests/lavado-finalizacion.test.js

# Ejecutar una suite específica
node --test Firebase.Tests/PreferredDeliveryUi.test.cjs
```

### 5.4 Pruebas de Navegador con Playwright

Para ejecutar las pruebas de interfaz gráfica en Chromium:

- **Directorio de ejecución**: Raíz del repositorio.
- **Requisitos**:
  - Dependencias de Node.js instaladas (`pnpm install`).
  - Binarios de Chromium instalados en `.local/playwright-browsers` (descarga inicial vía `pnpm test:e2e:install`).
  - Puerto `41783` desocupado.
- **Nota sobre scripts npm**: En el proyecto **no existe** un script `npm test`. Los comandos autorizados están definidos en `package.json` bajo el prefijo `test:e2e`:

```bash
# Ejecutar todas las pruebas de navegador en modo desatendido (headless)
pnpm test:e2e
# O mediante el wrapper directo de Node:
node scripts/playwright.cjs test

# Ejecutar en modo visible con interfaz de navegador (headed)
pnpm test:e2e:headed

# Ejecutar en interfaz interactiva de Playwright (UI Mode)
pnpm test:e2e:ui

# Abrir el informe HTML de la última ejecución
pnpm test:e2e:report

# Ejecutar un archivo de especificación puntual
node scripts/playwright.cjs test tests/browser/auditoria.spec.cjs
```

### 5.5 Prueba End-to-End Autenticada con Servidor en Vivo

Para ejecutar la verificación completa de autenticación y permisos con servidor real:

- **Directorio de ejecución**: Raíz del repositorio.
- **Requisitos**: Puertos `8189`, `9099` y `5200` libres; conexión de red si `flowbite.min.js` no ha sido descargado previamente en `.local/notification-proof/`.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/test-authenticated-notifications.ps1
```

---

## 6. Resultados y Evidencia

### 6.1 Verificación Ejecutada Durante Esta Actualización

Durante la presente revisión técnica (fecha: **17 de noviembre de 2026**), se ejecutaron las suites de pruebas JavaScript disponibles para constatar su comportamiento observable:

| Comando Ejecutado | Alcance | Total Tests | Aprobados | Fallidos | Omitidos | Duración Observada |
|---|---|---|---|---|---|---|
| `node --test Firebase.Tests/*.test.cjs` | 44 archivos de pruebas de componentes JS | 200 | 197 | 3 | 0 | 15.668 ms (~15,7 s) |
| `node --test tests/lavado-finalizacion.test.js` | Suite de modales de finalización y consumos | 21 | 21 | 0 | 0 | 192 ms (~0,2 s) |
| **TOTAL VERIFICADO EN VIVO** | **45 archivos JavaScript** | **221** | **218** | **3** | **0** | **~15,9 s** |

#### Detalle de los 3 Fallos Observados en Pruebas JavaScript:
1. **[Firebase.Tests/FormButtons.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/FormButtons.test.cjs#L40)** (`Servicio conserva atributos de formulario y ambos controles`):
   - *Causa*: El test esperaba únicamente los botones estándar de `submit` y `clear`. La vista `_ServicioForm.cshtml` incorporó botones adicionales para cerrar el editor de recetas (`closeRecipeEditor(true)` y `closeRecipeEditor(false)`), provocando una discrepancia en la aserción de igualdad estricta.
2. **[Firebase.Tests/WashDetailNotifications.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/WashDetailNotifications.test.cjs#L7)** (`wash detail uses shared transport and notifications while preserving token and form body`):
   - *Causa*: El test esperaba recibir el mensaje `'No permitido'` desde la respuesta mockeada. En el entorno de ejecución la llamada generó un error de red (`'Failed to fetch'`), capturado por el sistema de notificaciones.
3. **[Firebase.Tests/PurchaseUiCorrections.test.cjs](file:///C:/Users/Andre/Documents/Aplicacion-Lavadero/Firebase.Tests/PurchaseUiCorrections.test.cjs#L25)** (`S3 notas ocupa una columna de grilla adaptable`):
   - *Causa*: La vista `Views/Insumos/Cotizaciones.cshtml` incorporó los nuevos campos de `fechaEntregaPreferida` y `horaEntregaPreferida` inmediatamente antes del campo de `Notas`, alterando la coincidencia de expresión regular que esperaba el contenedor simple anterior.

### 6.2 Suites No Verificadas en Vivo y Limitaciones Ambientales

1. **Suite de Backend .NET (`dotnet test`)**:
   - *Estado*: No verificada en su totalidad durante esta tarea.
   - *Motivo*: Al ejecutar `dotnet test` en el entorno de prueba acotado, el proceso `vstest.console` experimentó un tiempo de espera de conexión (timeout de 90 segundos) al intentar comunicarse con el proceso `testhost` a través de sockets IPC locales. Además, las pruebas con emuladores (`run-isolated.ps1`) requieren procesos de fondo que no fueron forzados para cumplir con las directivas de seguridad del entorno.
2. **Suite de Navegador Playwright (`pnpm test:e2e`)**:
   - *Estado*: No verificada en su totalidad durante esta tarea.
   - *Motivo*: Al iniciar el CLI de Playwright en el entorno acotado, la herramienta intentó purgar y recrear los directorios de salida `test-results/` y `playwright-report/`, resultando en un error de permisos de sistema de archivos (`EPERM: operation not permitted, rmdir`). Por seguridad y para preservar el repositorio intacto, no se alteraron los permisos del sistema operativo.
3. **Prueba Autenticada E2E (`test-authenticated-notifications.ps1`)**:
   - *Estado*: No verificada en esta tarea.
   - *Motivo*: Requiere la descarga externa de librerías (`flowbite.min.js`) si no están en caché y el levantamiento simultáneo de tres servidores en segundo plano (Firestore, Auth y Kestrel).

### 6.3 Resumen de Cifras del Repositorio

Para contextualizar el progreso del repositorio y evitar confusiones con la documentación anterior:

- **Cifra Histórica de 494 Tests (5 de Diciembre, 2024)**:
  - Fase inicial del proyecto con solo 11 clases de prueba unitaria en `Firebase.Tests/Services/`. No contemplaba integración, seguridad RBAC, insumos ni frontend.
- **Cifra Histórica de 843 Tests Aislados (Registro de Titularidad y Patentes)**:
  - Registrada en archivos de log de corridas pasadas (`ownership-live-lifecycle.*.log`) con emuladores locales levantados para verificar las regresiones de titularidad de vehículos huérfanos.
- **Inventario Actual de Código de Prueba en el Repositorio (Noviembre 2026)**:
  - **906 métodos de prueba definidos en C# (.NET)** en 126 archivos de prueba (+ 2 helpers):
    - 390 métodos `[Fact]`
    - 134 métodos `[Theory]` (con múltiples casos parametrizados cada uno)
    - 381 métodos `[LocalFirestoreFact]` (pruebas contra Firestore Emulator)
    - 1 método `[LocalSeedFact]` (prueba contra Auth Emulator)
  - **221 casos de prueba JavaScript (`node:test`)** en 45 archivos de prueba.
  - **49 pruebas de navegador declaradas en Playwright** en 17 archivos `.spec.cjs`.
  - **1 suite completa de integración E2E autenticada** con Playwright, Kestrel y emuladores.

---

## 7. Conclusiones y Recomendaciones de Calidad

1. **Robustez del Módulo de Notificaciones**:
   - El nuevo módulo `ClienteNotificationService` introduce una sólida arquitectura de entrega dual (WhatsApp/Email) con salvaguardas explícitas: normalización que impide clientes sin vías de contacto, cálculo transparente de etapas siguientes, anticipación de vehículos listos para retirar, idempotencia estricta ante finalizaciones repetidas y protección contra fugas de credenciales en logs de transporte.
2. **Higiene en Pruebas de Integración y Transacciones**:
   - Se destaca la cobertura de tolerancia a fallos en `LavadoCreationPricingRefactorTests.cs`: si la auditoría de la notificación experimenta un fallo temporal, la operación comercial principal y el envío de notificaciones continúan con éxito sin bloquear al usuario.
3. **Mantenimiento y Sincronización de Pruebas de Frontend**:
   - Se recomienda actualizar las expresiones regulares en `PurchaseUiCorrections.test.cjs` para reflejar los campos de entrega preferida, y en `FormButtons.test.cjs` para los botones del editor de recetas.
4. **Adopción de Reportes Formales de Cobertura**:
   - Se aconseja instrumentar un pipeline continuo que ejecute `dotnet test --collect:"XPlat Code Coverage"` junto con emuladores para generar informes objetivos de cobertura porcentual por ramas y líneas.
