using Microsoft.AspNetCore.Mvc;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;

using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Controllers
{
    [ApiController]
    [Route("api/publicaciones")]
    public class PublicacionesController : ControllerBase
    {
        private readonly IPublicacionServicio _publicacionServicio;
        private readonly ApplicationDbContext _context;

        public PublicacionesController(
            IPublicacionServicio publicacionServicio,
            ApplicationDbContext context)
        {
            _publicacionServicio = publicacionServicio;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var publicaciones = await _context.Publicaciones
                .Include(p => p.Lugar)
                .Include(p => p.Usuario)
                .Where(p => !p.EstaOculto)
                .ToListAsync();

            var result = publicaciones.Select(p => new {
                id = p.Id.ToString(),
                title = p.Titulo,
                year = p.Fecha.Year,
                author = p.Usuario?.Username ?? "Desconocido",
                place = p.Lugar?.Nombre ?? "",
                lat = p.Lugar?.Coordenadas.Y ?? 0,
                lng = p.Lugar?.Coordenadas.X ?? 0,
                description = p.Descripcion,
                category = p.Categoria.ToString().Replace("Recuerdos_Personales", "Personales"),
                source = "db",
                media = string.IsNullOrEmpty(p.UrlMultimedia) ? null : new {
                    url = p.UrlMultimedia,
                    kind = p.TipoMultimedia == MultimediaEnum.Video ? "video" : "image"
                }
            });

            return Ok(result);
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

        [HttpPost("compartir")]
        public async Task<IActionResult> Compartir([FromBody] CompartirPublicacionRequest request)
        {
            var dto = new CompartirPublicacionDto
            {
                PublicacionOriginalId = request.PublicacionOriginalId,
                AutorId = request.AutorId,
                Descripcion = request.Descripcion ?? string.Empty
            };

            try
            {
                var id = await _publicacionServicio.Compartir(dto);

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

