/** Shared transport. Callers retain response parsing and domain-specific decisions. */
(function () {
    'use strict';

    function enviar(input, options = {}) {
        const url = new URL(input instanceof Request ? input.url : input, document.baseURI);
        const sameOrigin = url.origin === location.origin;
        const headers = new Headers(input instanceof Request ? input.headers : undefined);
        new Headers(options.headers).forEach((value, key) => headers.set(key, value));
        if (sameOrigin && !headers.has('X-Requested-With')) headers.set('X-Requested-With', 'XMLHttpRequest');
        // Preserve method, body (including antiforgery form fields), signal and fetch semantics.
        return fetch(input, { ...options, headers });
    }

    async function json(input, options) {
        const response = await enviar(input, options);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json();
    }

    async function texto(input, options) {
        const response = await enviar(input, options);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.text();
    }

    async function resultado(input, options) {
        const response = await enviar(input, options);
        const data = await response.json().catch(() => null);
        return { response, data };
    }

    function mensajeError(error, fallback = 'No se pudo completar la solicitud.') {
        if (error?.message === 'HTTP 401') return 'La sesión venció. Volvé a iniciar sesión.';
        if (error?.message === 'HTTP 403') return 'Acceso denegado. No tenés permiso para realizar esta acción.';
        return fallback;
    }

    const filters = new Map();
    async function filtrar(key, apply) {
        const previous = filters.get(key);
        if (previous) previous.dismiss();
        const state = { dismiss: window.Notificaciones.cargando('Aplicando filtros…') };
        filters.set(key, state);
        try {
            await apply();
            if (filters.get(key) !== state) return false;
            window.Notificaciones.exito('Filtros aplicados.');
            return true;
        } catch (error) {
            if (filters.get(key) === state && error.name !== 'AbortError') {
                window.Notificaciones.error('No se pudieron aplicar los filtros.');
            }
            return false;
        } finally {
            state.dismiss();
            if (filters.get(key) === state) filters.delete(key);
        }
    }

    window.Peticiones = Object.freeze({ enviar, json, texto, resultado, mensajeError, filtrar });
})();
