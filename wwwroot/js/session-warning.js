/**
 * Sistema de notificación de expiración de sesión
 * Avisa 5 minutos antes de que expire la sesión por inactividad o duración máxima
 */

class SessionWarningManager {
    constructor() {
        this.warningTime = 5 * 60 * 1000; // 5 minutos en milisegundos
        this.checkInterval = 30 * 1000; // Verificar cada 30 segundos
        this.maxDurationMinutes = null;
        this.inactivityMinutes = null;
        this.loginTime = null;
        this.lastActivityTime = null;
        this.checkTimer = null;
        this.warningShown = false;
        this.notificationElement = null;
        this.countdownTimer = null;

        this.init();
    }

    /**
     * Inicializa el gestor de advertencias de sesión
     */
    init() {
        // Obtener configuración desde el servidor
        this.loadSessionConfig().then(() => {
            this.createNotificationElement();
            this.startMonitoring();
            this.trackUserActivity();
        });
    }

    /**
     * Carga la configuración de sesión desde el servidor
     */
    async loadSessionConfig() {
        try {
            const response = await fetch('/api/session-config', {
                method: 'GET',
                headers: {
                    'Content-Type': 'application/json'
                }
            });

            if (response.ok) {
                const config = await response.json();
                this.maxDurationMinutes = config.maxDurationMinutes;
                this.inactivityMinutes = config.inactivityMinutes;
                this.loginTime = new Date(config.loginTime);
                this.lastActivityTime = new Date();

                console.log('Configuración de sesión cargada:', config);
            }
        } catch (error) {
            console.error('Error al cargar configuración de sesión:', error);
            // Valores por defecto
            this.maxDurationMinutes = 480; // 8 horas
            this.inactivityMinutes = 15;
            this.loginTime = new Date();
            this.lastActivityTime = new Date();
        }
    }

    /**
     * Crea el elemento de notificación para mostrar advertencias
     */
    createNotificationElement() {
        // Crear contenedor de notificación con Tailwind CSS
        this.notificationElement = document.createElement('div');
        this.notificationElement.id = 'session-warning-notification';
        this.notificationElement.className = 'fixed bottom-5 right-5 w-96 max-w-[calc(100vw-2.5rem)] bg-yellow-50 dark:bg-yellow-900 rounded-lg shadow-lg border-2 border-yellow-400 dark:border-yellow-600 z-[9999] transform translate-x-[450px] transition-transform duration-300 ease-out opacity-0 pointer-events-none';
        this.notificationElement.innerHTML = `
            <div class="p-4 flex gap-3">
                <div class="flex-shrink-0">
                    <div class="w-10 h-10 rounded-full bg-gradient-to-br from-yellow-400 to-yellow-600 flex items-center justify-center animate-pulse shadow-lg">
                        <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2.5" stroke="currentColor" class="w-6 h-6 text-yellow-950 dark:text-yellow-100">
                            <path stroke-linecap="round" stroke-linejoin="round" d="M12 6v6h4.5m4.5 0a9 9 0 11-18 0 9 9 0 0118 0z" />
                        </svg>
                    </div>
                </div>
                <div class="flex-1 min-w-0">
                    <h4 class="text-sm font-semibold text-yellow-900 dark:text-yellow-100 mb-1">Sesión por expirar</h4>
                    <p class="text-sm text-yellow-800 dark:text-yellow-200" id="session-warning-message"></p>
                    <p class="text-xs text-yellow-700 dark:text-yellow-300 mt-2 font-medium" id="session-countdown"></p>
                </div>
                <button onclick="sessionWarning.dismissNotification()" class="flex-shrink-0 text-yellow-600 hover:text-yellow-800 dark:text-yellow-400 dark:hover:text-yellow-200 transition-colors">
                    <svg xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke-width="2" stroke="currentColor" class="w-5 h-5">
                        <path stroke-linecap="round" stroke-linejoin="round" d="M6 18L18 6M6 6l12 12" />
                    </svg>
                </button>
            </div>
        `;

        document.body.appendChild(this.notificationElement);
    }

    /**
     * Inicia el monitoreo periódico del estado de la sesión
     */
    startMonitoring() {
        // Verificar estado de la sesión periódicamente
        this.checkTimer = setInterval(() => {
            this.checkSessionStatus();
        }, this.checkInterval);
    }

    /**
     * Configura el seguimiento de actividad del usuario
     */
    trackUserActivity() {
        // Eventos que indican actividad del usuario
        const activityEvents = ['mousedown', 'keydown', 'scroll', 'touchstart', 'click'];

        activityEvents.forEach(event => {
            document.addEventListener(event, () => {
                this.lastActivityTime = new Date();

                // Si la notificación está visible y el usuario está activo, ocultarla
                if (this.warningShown) {
                    this.dismissNotification();
                }
            }, { passive: true });
        });
    }

    /**
     * Verifica el estado actual de la sesión y muestra advertencias si es necesario
     */
    checkSessionStatus() {
        if (!this.maxDurationMinutes || !this.inactivityMinutes || !this.loginTime) {
            return;
        }

        const now = new Date();

        // Calcular tiempo transcurrido desde el login
        const sessionDurationMs = now - this.loginTime;
        const maxDurationMs = this.maxDurationMinutes * 60 * 1000;
        const timeUntilMaxExpiration = maxDurationMs - sessionDurationMs;

        // Calcular tiempo de inactividad
        const inactivityMs = now - this.lastActivityTime;
        const maxInactivityMs = this.inactivityMinutes * 60 * 1000;
        const timeUntilInactivityExpiration = maxInactivityMs - inactivityMs;

        // Determinar qué expirará primero
        const minTimeUntilExpiration = Math.min(timeUntilMaxExpiration, timeUntilInactivityExpiration);

        // Mostrar notificación si falta menos de 5 minutos
        if (minTimeUntilExpiration > 0 && minTimeUntilExpiration <= this.warningTime && !this.warningShown) {
            const expirationReason = timeUntilMaxExpiration < timeUntilInactivityExpiration
                ? 'duration'
                : 'inactivity';

            this.showWarning(minTimeUntilExpiration, expirationReason);
        }

        // Actualizar countdown si la notificación está visible
        if (this.warningShown && minTimeUntilExpiration > 0) {
            this.updateCountdown(minTimeUntilExpiration);
        }

        // Si el tiempo ya expiró, redirigir al login
        if (minTimeUntilExpiration <= 0) {
            this.handleSessionExpired(timeUntilMaxExpiration < timeUntilInactivityExpiration
                ? 'duration'
                : 'inactivity');
        }
    }

    /**
     * Muestra la notificación de advertencia de expiración de sesión
     * @param {number} timeRemaining - Tiempo restante en milisegundos
     * @param {string} reason - Razón de la expiración ('duration' o 'inactivity')
     */
    showWarning(timeRemaining, reason) {
        const messageElement = document.getElementById('session-warning-message');

        const message = reason === 'duration'
            ? 'Su sesión está por alcanzar el tiempo máximo permitido y se cerrará automáticamente.'
            : 'Su sesión se cerrará por inactividad. Interactúe con la aplicación para mantenerla activa.';

        messageElement.textContent = message;

        // Mostrar notificación con animación
        this.notificationElement.classList.remove('translate-x-[450px]', 'opacity-0', 'pointer-events-none');
        this.notificationElement.classList.add('translate-x-0', 'opacity-100', 'pointer-events-auto');
        this.warningShown = true;

        // Iniciar actualización de countdown
        this.updateCountdown(timeRemaining);

        console.log(`⚠️ Advertencia de sesión: ${message}`);
    }

    /**
     * Actualiza el contador de tiempo restante en la notificación
     * @param {number} timeRemaining - Tiempo restante en milisegundos
     */
    updateCountdown(timeRemaining) {
        const countdownElement = document.getElementById('session-countdown');
        if (!countdownElement) return;

        const minutes = Math.floor(timeRemaining / 60000);
        const seconds = Math.floor((timeRemaining % 60000) / 1000);

        countdownElement.textContent = `Tiempo restante: ${minutes}:${seconds.toString().padStart(2, '0')}`;
    }

    /**
     * Oculta la notificación de advertencia
     */
    dismissNotification() {
        if (!this.notificationElement) return;

        this.notificationElement.classList.remove('translate-x-0', 'opacity-100', 'pointer-events-auto');
        this.notificationElement.classList.add('translate-x-[450px]', 'opacity-0', 'pointer-events-none');
        this.warningShown = false;

        if (this.countdownTimer) {
            clearInterval(this.countdownTimer);
            this.countdownTimer = null;
        }
    }

    /**
     * Maneja la expiración de la sesión redirigiendo al login
     * @param {string} reason - Razón de la expiración ('duration' o 'inactivity')
     */
    handleSessionExpired(reason) {
        clearInterval(this.checkTimer);
        const redirectUrl = reason === 'duration'
            ? '/Login/Index?expired=duration'
            : '/Login/Index?expired=inactivity';

        window.location.href = redirectUrl;
    }

    /**
     * Destruye el gestor de advertencias y limpia recursos
     */
    destroy() {
        if (this.checkTimer) {
            clearInterval(this.checkTimer);
        }
        if (this.countdownTimer) {
            clearInterval(this.countdownTimer);
        }
        if (this.notificationElement) {
            this.notificationElement.remove();
        }
    }
}

// Inicializar el gestor cuando el DOM esté listo
let sessionWarning;
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', () => {
        sessionWarning = new SessionWarningManager();
    });
} else {
    sessionWarning = new SessionWarningManager();
}
