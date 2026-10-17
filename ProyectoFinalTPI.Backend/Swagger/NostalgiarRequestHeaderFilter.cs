using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ProyectoFinalTPI.Backend.Swagger;

/// <summary>
/// Inyecta el header X-Nostalgiar-Request en todos los endpoints de Swagger
/// para replicar el comportamiento del frontend y poder probar la API sin
/// configuración manual adicional.
/// </summary>
public class NostalgiarRequestHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "X-Nostalgiar-Request",
            In = ParameterLocation.Header,
            Required = false,
            Schema = new OpenApiSchema
            {
                Type = "string",
                Default = new OpenApiString("1")
            },
            Description = "Requerido por los endpoints de autenticación. Valor: 1"
        });
    }
}
