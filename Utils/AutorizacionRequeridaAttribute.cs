using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FirebaseLoginCustom.Utils
{
    /// <summary>
    /// Atributo de autorización personalizado que requiere que el usuario esté autenticado.
    /// Si el usuario no está autenticado, lo redirige a la página de login.
    /// </summary>
    /// <remarks>
    /// Este atributo se puede aplicar a controladores o acciones específicas para protegerlas.
    /// Ejemplo de uso: [AutorizacionRequerida] en la clase o método del controlador.
    /// Actualmente se usa en LavadosController para proteger todas las acciones del controlador.
    /// </remarks>
    public class AutorizacionRequeridaAttribute : Attribute, IAuthorizationFilter
    {
        /// <summary>
        /// Método que se ejecuta para verificar la autorización del usuario.
        /// </summary>
        /// <param name="filterContext">Contexto del filtro de autorización</param>
        public void OnAuthorization(AuthorizationFilterContext filterContext)
        {
            if (!filterContext.HttpContext.User.Identity.IsAuthenticated)
            {
                filterContext.Result = new RedirectToRouteResult(new RouteValueDictionary(new { controller = "Login", action = "Index" }));
            }
        }
    }
}
