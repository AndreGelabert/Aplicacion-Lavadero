# 🚗 LavaFácil - Sistema de Gestión de Servicios y Turnos para Lavaderos (Sigue en construcción)

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%20C%23-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Blazor](https://img.shields.io/badge/Blazor-WebAssembly%20%2F%20Server-512BD4?logo=blazor&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
[![Firebase](https://img.shields.io/badge/Firebase-Auth%20%26%20Firestore-FFCA28?logo=firebase&logoColor=black)](https://firebase.google.com/)
[![WhatsApp API](https://img.shields.io/badge/WhatsApp-Cloud%20API-25D366?logo=whatsapp&logoColor=white)](https://developers.facebook.com/docs/whatsapp/cloud-api)
[![Metodología](https://img.shields.io/badge/Metodolog%C3%ADa-Proceso%20Unificado%20(UP)-blue)](#metodología-y-diseño)
[![Estado](https://img.shields.io/badge/Estado-En%20Desarrollo%20Avanzado-brightgreen)](#)

---

## 📌 Descripción del Proyecto

**LavaFácil** es una solución integral y omnicanal diseñada para optimizar, centralizar y automatizar los procesos operativos y administrativos de lavaderos de vehículos.

El sistema resuelve las problemáticas tradicionales de la gestión manual (solapamiento de turnos, falta de visibilidad en tiempos operativos, ausentismo y desorganización en caja) mediante una plataforma web administrativa y un **bot conversacional inteligente integrado con WhatsApp**, permitiendo a los clientes interactuar de forma autónoma las 24 horas del día.

---

## ✨ Características Principales

### 👥 Gestión de Clientes y Flota de Vehículos
- **Alta Transaccional Unificada:** Obligatoriedad de registrar al menos un vehículo al dar de alta un cliente nuevo.
- **Soporte Multipropietario (Co-propiedad):** Múltiples clientes pueden asociar un mismo vehículo utilizando claves de seguridad criptográficas (SHA-256).
- **Gestión en Cascada de Bajas Lógicas:** Al desactivar un cliente, el sistema analiza su flota desactivando automáticamente vehículos unipersonales y preservando vehículos compartidos con otros dueños activos.
- **Autocompletado de Rodados:** Integración con catálogo y formato de patentes según tipo de vehículo (Autos, Camionetas, Motos).

### 🛠️ Configuración de Servicios y Paquetes
- **Catálogo Dinámico:** Parametrización de precios y tiempos estimados según el tipo de vehículo.
- **Etapas Operativas:** Definición y seguimiento secuencial de fases de lavado (e.g., Lavado exterior, Aspirado, Encerado).
- **Paquetes con Descuento:** Creación de promociones combinadas con cálculo automático de tarifas y duración acumulada.

### 🚿 Operatoria de Lavados y Control de Tiempos
- **Monitoreo en Tiempo Real:** Seguimiento de estados (*EnProceso*, *Realizado*, *RealizadoParcialmente*, *Cancelado*).
- **Control de Etapas y Servicios:** Inicio y finalización granular de tareas por parte de los operarios asignados.
- **Retiro y Entrega:** Registro formal del cliente que retira físicamente la unidad del establecimiento.

### 📅 Planificación Inteligente de Turnos
- **Asignación sin Solapamiento:** Algoritmo de cálculo de franjas horarias libres según capacidad máxima concurrente y duración estimada del vehículo/servicio.
- **Reorganización Dinámica ante Cancelaciones:** Detección de huecos de agenda y propuesta automática de adelanto de turno a clientes posteriores vía WhatsApp.

### 🤖 Bot Omnicanal de WhatsApp (Cloud API)
- **Atención Automatizada 24/7:** Registro guiado de clientes, alta y vinculación de vehículos.
- **Reserva y Cancelación de Turnos:** Autoservicio conversacional para agendar y cancelar citas.
- **Notificaciones Transaccionales:** Avisos automáticos de turnos próximos, avance de etapas de lavado y notificación con liquidación de saldo para el retiro del vehículo.
- **Derivación Humana:** Protocolo de pausa del bot y traspaso a operadores del lavadero.

### 💳 Finanzas, Reportes y Auditoría
- **Cobranzas:** Registro de pagos totales y parciales (señas), admitiendo múltiples medios de pago.
- **Auditoría Inmutable:** Registro automático de trazabilidad (*AuditLog*) para todas las operaciones críticas.
- **Estadísticas y Reportes:** Indicadores clave de rendimiento (KPIs), productividad de empleados y exportación de informes a **PDF** y **Excel**.

---

## 🏗️ Arquitectura y Tecnologías

El sistema está desarrollado siguiendo las buenas prácticas de la arquitectura en capas y principios SOLID:

- **Frontend / UI:** Blazor (.NET 8) con interfaz reactiva basada en componentes y modales contextuales.
- **Backend / Core:** C# / .NET con controladores REST y servicios desacoplados.
- **Autenticación y Seguridad:** Firebase Authentication (OAuth Google & Email/Password) con control de acceso basado en roles (**RBAC**).
- **Persistencia de Datos:** Firestore / Firebase Cloud Database (NoSQL estructurado) y almacenamiento de sesiones temporales.
- **Integraciones Externas:**
  - Meta WhatsApp Cloud API (Webhooks seguros con verificación de firma).
  - CarQuery API / Validadores de patentes.

---

## 🚀 Instalación y Configuración Local

### Prerrequisitos
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) o superior.
- [Visual Studio 2022 / 2026](https://visualstudio.microsoft.com/) con la carga de trabajo *Desarrollo web y ASP.NET*.
- Proyecto configurado en [Firebase Console](https://console.firebase.google.com/).
- Cuenta en [Meta for Developers](https://developers.facebook.com/) con WhatsApp Cloud API configurada.

### Pasos de Instalación

1. **Clonar el repositorio:**
```bash
git clone [https://github.com/tu-usuario/LavaFacil.git](https://github.com/tu-usuario/LavaFacil.git)
cd LavaFacil
```

2. **Configurar variables de entorno y secretos:**
Crear o modificar el archivo `appsettings.Development.json` en `src/LavaFacil.Server`:
```json
{
  "Firebase": {
    "ProjectId": "tu-proyecto-firebase",
    "ApiKey": "tu-api-key",
    "AuthDomain": "tu-proyecto.firebaseapp.com"
  },
  "WhatsApp": {
    "ApiUrl": "[https://graph.facebook.com/v18.0](https://graph.facebook.com/v18.0)",
    "PhoneNumberId": "tu-phone-number-id",
    "AccessToken": "tu-permanent-access-token",
    "VerifyToken": "tu-token-de-verificacion-webhook"
  },
  "AppSettings": {
    "JwtSecret": "clave-secreta-para-tokens-internos"
  }
}
```

3. **Restaurar dependencias:**
```bash
dotnet restore
```

4. **Compilar y Ejecutar la Solución:**
```bash
dotnet build
dotnet run --project src/LavaFacil.Server
```

5. **Acceder a la aplicación:**
Abrir el navegador web e ingresar a `https://localhost:7001` (o el puerto configurado).

---

## 📖 Metodología y Diseño

El proyecto se desarrolla bajo el marco del **Proceso Unificado (UP)**:
- **Requisitos:** 93 Casos de Uso del Sistema detallados, matriz de rastreabilidad y modelado de requisitos funcionales/no funcionales.
- **Análisis:** Modelo Conceptual con conceptos idóneos, Diagramas de Secuencia del Sistema (SSD) y Contratos de Operación formalizados.
- **Diseño e Implementación:** Diagramas de Clases de Diseño, Casos de Uso Reales e interacción desacoplada.

---

## 👨‍💻 Autor y Contexto Académico

- **Autor:** André Gelabert
- **Carreras:** Analista en Sistemas de Computación (ASC) | Licenciatura en Sistemas de Información (LSI)
- **Institución:** Facultad de Ciencias Exactas, Químicas y Naturales (FCEQyN) – Universidad Nacional de Misiones (UNaM)
- **Cátedras:** Trabajo Final (ASC) / Proyecto Software (LSI) - 2026
