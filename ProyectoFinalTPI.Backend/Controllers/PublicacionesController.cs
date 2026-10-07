using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;

namespace ProyectoFinalTPI.Backend.Controllers
{
    [ApiController]
    [Route("api/publicaciones")]
    public class PublicacionesController : ControllerBase
    {
        private readonly IPublicacionServicio _publicacionServicio;

        public PublicacionesController(
            IPublicacionServicio publicacionServicio)
        {
            _publicacionServicio = publicacionServicio;
        }

        [HttpPost]
        public async Task<IActionResult> Crear([FromForm] CrearPublicacionRequest request)
        {
            using var stream = request.Archivo?.OpenReadStream();

            var dto = new CrearPublicacionDto
            {
                Titulo = request.Titulo,
                Descripcion = request.Descripcion,
                Fecha = request.Fecha,

                Categoria = request.Categoria,

                NombreLugar = request.NombreLugar,

                Latitud = request.Latitud,
                Longitud = request.Longitud,

                TipoMultimedia = request.TipoMultimedia,

                Archivo = stream,
                NombreArchivo = request.Archivo?.FileName,
                UrlArchivo = request.UrlArchivo,

                AutorId = request.AutorId
            };

            try
            {
                var id = await _publicacionServicio.Crear(dto);

                return Created(
                    $"/api/publicaciones/{id}",
                    new { id });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(new { mensaje = ex.Message });
            }
        }
    }
}
