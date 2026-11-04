# Mejoras futuras del sistema

Este archivo conserva trabajo pospuesto. Las tareas siguientes pertenecen a la **integración del bot de WhatsApp con el sistema del lavadero**. No están implementadas ni autorizadas para comenzar automáticamente.

## Contexto técnico

Meta envía mensajes y notificaciones a `POST /api/whatsapp/webhook`, implementado en `Controllers/WhatsAppWebhookController.cs`. El controlador llama a `WhatsAppFlowService` y sus archivos parciales en `Services/` para ejecutar las conversaciones de registro de clientes y gestión de vehículos. `WhatsAppSessionService` guarda el estado de conversación en Firestore y `MetaWhatsAppService` llama a la API de Meta para enviar respuestas.

Actualmente el mensaje se procesa dentro de la misma petición HTTP. Los errores detectados pueden producir una respuesta distinta de 200, y Meta puede volver a entregar el evento. No hay una bandeja durable de eventos ni una garantía general de procesamiento único. Tampoco hay una transacción que abarque los cambios de negocio y la intención de responder por WhatsApp.

Los resultados explícitos de los servicios y los logs correlacionados son mejoras de diagnóstico: **no sustituyen las garantías de persistencia y recuperación descritas abajo**.

## [ ] WA-01 — Recepción durable y deduplicación de mensajes entrantes

**Problema que resuelve:** un reinicio puede interrumpir un mensaje en procesamiento. Una entrega repetida de Meta puede ejecutar dos veces la misma acción, enviar respuestas duplicadas o interpretar el mensaje desde un estado de conversación distinto.

**Componentes que revisar:** `WhatsAppWebhookController`, los modelos de eventos en `Models/WhatsApp/`, `WhatsAppFlowService`, `WhatsAppSessionService` y un futuro procesador de eventos pendientes. Evaluar el almacenamiento durable en Firestore antes de elegir otra infraestructura.

**Trabajo propuesto:**
- Validar el evento y guardarlo de forma durable antes de responder HTTP 200. Si no se pudo guardar, no confirmar su recepción como exitosa.
- Procesar los eventos guardados fuera de la petición del webhook. Una cola únicamente en memoria no alcanza: debe sobrevivir a reinicios.
- Para mensajes entrantes, usar `messages[].id` como clave de deduplicación, delimitada por la cuenta/número correspondiente. Otros eventos necesitan una política propia; no asumir que todos tienen ese identificador.
- Tomar el trabajo mediante una operación atómica: comprobar y luego guardar por separado permite que dos procesadores ejecuten el mismo mensaje.
- Registrar estados pendiente, en procesamiento, completado y fallido. Diseñar recuperación del trabajo abandonado por un proceso interrumpido y reintentos acotados según el tipo de falla.
- Mantener el orden necesario de las conversaciones: dos mensajes del mismo cliente no deben modificar simultáneamente el estado ni procesarse fuera de orden sin una política explícita.
- Definir retención y protección de los eventos almacenados, porque pueden contener datos personales. No copiarlos completos a los logs.

**Criterios de aceptación:**
- Entregar el mismo mensaje dos veces, incluso simultáneamente, no repite sus efectos de negocio.
- Reiniciar después del guardado y antes del procesamiento no pierde el mensaje.
- Un fallo al guardar no devuelve 200; una entrega repetida ya aceptada se reconoce sin ejecutarla otra vez.
- La recuperación de un procesamiento interrumpido y el orden por conversación tienen pruebas aisladas, sin mensajes a clientes reales.
- El diagnóstico permite distinguir recepción, procesamiento y fallo sin exponer contenido personal.

## [ ] WA-02 — Cambios de negocio consistentes y respuestas pendientes mediante outbox

**Problema que resuelve:** el flujo puede guardar cambios del cliente, vehículo o sesión y luego fallar al enviar la respuesta. También puede ocurrir lo contrario: Meta acepta el envío, pero la aplicación pierde su respuesta y no sabe si repetirlo. Reintentar todo el flujo puede duplicar operaciones.

**Componentes que revisar:** manejadores de registro, edición y vehículos en `Services/WhatsAppFlowService*.cs`, `WhatsAppSessionService`, los servicios de negocio y escrituras Firestore que invoquen, `MetaWhatsAppService` y el futuro procesador de WA-01.

**Trabajo propuesto:**
- Identificar, por cada operación del bot, qué cambios deben ser atómicos. No envolver indiscriminadamente toda la conversación en una transacción.
- Cuando corresponda, guardar en una misma transacción Firestore los cambios de negocio, el avance de sesión, el resultado del evento y la intención de enviar la respuesta. Esa intención persistida es la **outbox** o bandeja de salida.
- Enviar las respuestas pendientes fuera de la transacción y registrar su resultado para recuperación y diagnóstico.
- No llamar a la API de WhatsApp dentro del callback de una transacción Firestore: Firestore puede ejecutar ese callback varias veces ante conflictos.
- Clasificar fallas permanentes, temporales y de resultado incierto. No reintentar indefinidamente errores de credenciales ni repetir a ciegas un envío cuya aceptación se desconoce.
- Definir cómo observar y resolver una salida pendiente o incierta sin volver a ejecutar la operación de negocio.

**Criterios de aceptación:**
- Un fallo de la transacción no deja cambios parciales ni respuestas pendientes que anuncien una operación no guardada.
- Si los cambios se guardaron pero el envío falló, puede retomarse la respuesta sin repetir la modificación del cliente, vehículo o sesión.
- Reiniciar después de guardar la outbox no pierde la intención de respuesta.
- Las pruebas cubren conflictos de transacción, fallos de envío y aceptación de resultado incierto, sin llamadas reales a Meta.
- El diseño documenta sus límites: una transacción local no incluye el servicio de Meta y la outbox **no garantiza por sí sola entrega exactamente una vez**.

**Relación entre las tareas:** diseñar WA-02 junto con los estados y la recuperación de WA-01. La deduplicación de entrada y la outbox resuelven problemas distintos; ninguna reemplaza a la otra.
