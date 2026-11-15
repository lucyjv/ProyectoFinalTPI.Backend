using Microsoft.AspNetCore.Mvc.Filters;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.Categorias;
using ProyectoFinalTPI.Backend.Extensions;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Filters;

[AttributeUsage(AttributeTargets.Method)]
public sealed class PermisoCategoriaAttribute(PermisoCuenta permiso) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var id = context.HttpContext.User.ObtenerUsuarioId();
        if (!id.HasValue) throw new UnauthorizedAccessException("Debés iniciar sesión.");
        var servicio = context.HttpContext.RequestServices.GetRequiredService<ICategoriaServicio>();
        await servicio.ExigirPermiso(id.Value, permiso, context.HttpContext.RequestAborted);
        await next();
    }
}