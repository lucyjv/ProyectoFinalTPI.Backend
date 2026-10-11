using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ProyectoFinalTPI.Backend.Controllers;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests;

public class EtiquetadoPublicacionControllerTests
{
    private static readonly JsonSerializerOptions OpcionesJson = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [Fact]
    public async Task Etiquetar_usuarios_nuevos_devuelve_201_y_cantidad_creada()
    {
        var servicio = new EtiquetadoFake { CantidadCreada = 2 };
        var controller = CrearController(servicio);

        var resultado = await controller.EtiquetarUsuarios(
            25,
            new EtiquetarUsuariosRequest { UsuariosIds = new List<int> { 10, 20 } },
            CancellationToken.None);
        var respuesta = Assert.IsType<ObjectResult>(resultado);

        Assert.Equal((int)HttpStatusCode.Created, respuesta.StatusCode);
        var contenido = JsonSerializer.Serialize(respuesta.Value, OpcionesJson);
        Assert.Contains("Se etiquetaron los usuarios", contenido);
        Assert.Contains("\"cantidadEtiquetasNuevas\":2", contenido);
        Assert.Equal(25, servicio.UltimaSolicitud.PublicacionId);
        Assert.Equal(new[] { 10, 20 }, servicio.UltimaSolicitud.UsuariosIds);
    }

    [Fact]
    public async Task Etiquetas_existentes_devuelven_200()
    {
        var controller = CrearController(new EtiquetadoFake { CantidadCreada = 0 });

        var resultado = await controller.EtiquetarUsuarios(
            25,
            new EtiquetarUsuariosRequest { UsuariosIds = new List<int> { 10 } },
            CancellationToken.None);
        var respuesta = Assert.IsType<OkObjectResult>(resultado);
        var contenido = JsonSerializer.Serialize(respuesta.Value, OpcionesJson);

        Assert.Equal((int)HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Contains("usuarios ya etiquetados en la publicacion", contenido);
        Assert.Contains("\"cantidadEtiquetasNuevas\":0", contenido);
    }

    [Theory]
    [InlineData("Debe indicar al menos un usuario para etiquetar", 25, new int[] { })]
    [InlineData("Los IDs de usuarios deben ser mayores que cero.", 25, new int[] { 0 })]
    [InlineData("El ID de la publicación debe ser mayor que cero", 0, new int[] { 10 })]
    public async Task Entrada_invalida_devuelve_400(
        string mensaje,
        int publicacionId,
        int[] usuariosIds)
    {
        var servicio = new EtiquetadoFake
        {
            Error = new ArgumentException(mensaje)
        };
        var controller = CrearController(servicio);

        var resultado = await controller.EtiquetarUsuarios(
            publicacionId,
            new EtiquetarUsuariosRequest { UsuariosIds = usuariosIds.ToList() },
            CancellationToken.None);
        var respuesta = Assert.IsType<BadRequestObjectResult>(resultado);

        Assert.Equal((int)HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(mensaje, JsonSerializer.Serialize(respuesta.Value, OpcionesJson));
        Assert.True(servicio.FueInvocado);
    }

    [Theory]
    [InlineData("La publicación no existe o no está disponible.")]
    [InlineData("No se encontraron los usuarios con ID: 99.")]
    public async Task Entidad_inexistente_devuelve_404_con_mensaje(string mensaje)
    {
        var controller = CrearController(new EtiquetadoFake
        {
            Error = new KeyNotFoundException(mensaje)
        });

        var resultado = await controller.EtiquetarUsuarios(
            25,
            new EtiquetarUsuariosRequest { UsuariosIds = new List<int> { 10 } },
            CancellationToken.None);
        var respuesta = Assert.IsType<NotFoundObjectResult>(resultado);

        Assert.Equal((int)HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains(mensaje, JsonSerializer.Serialize(respuesta.Value, OpcionesJson));
    }

    [Fact]
    public async Task Error_inesperado_devuelve_500_sin_exponer_detalles()
    {
        var controller = CrearController(new EtiquetadoFake
        {
            Error = new InvalidOperationException("detalle interno")
        });

        var resultado = await controller.EtiquetarUsuarios(
            25,
            new EtiquetarUsuariosRequest { UsuariosIds = new List<int> { 10 } },
            CancellationToken.None);
        var respuesta = Assert.IsType<ObjectResult>(resultado);
        var contenido = JsonSerializer.Serialize(respuesta.Value, OpcionesJson);

        Assert.Equal((int)HttpStatusCode.InternalServerError, respuesta.StatusCode);
        Assert.Contains("Ocurrio un error al etiquetar usuarios en la publicacion", contenido);
        Assert.DoesNotContain("detalle interno", contenido);
    }

    private static PublicacionesController CrearController(EtiquetadoFake servicio)
    {
        return new PublicacionesController(
            null!,
            servicio,
            NullLogger<PublicacionesController>.Instance);
    }

    private sealed class EtiquetadoFake : IEtiquetadoPublicacionServicio
    {
        public int CantidadCreada { get; set; }
        public Exception? Error { get; set; }
        public (int PublicacionId, int[] UsuariosIds) UltimaSolicitud { get; private set; }
        public bool FueInvocado { get; private set; }

        public Task<int> EtiquetarUsuarios(
            int publicacionId,
            IReadOnlyCollection<int> usuariosIds,
            CancellationToken cancellationToken = default)
        {
            FueInvocado = true;
            UltimaSolicitud = (publicacionId, usuariosIds.ToArray());

            return Error is null
                ? Task.FromResult(CantidadCreada)
                : Task.FromException<int>(Error);
        }
    }
}
