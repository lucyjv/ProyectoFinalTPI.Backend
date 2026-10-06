using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Dtos;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio
{
    public class PublicacionServicio : IPublicacionServicio
    {
        private readonly IPublicacionRepositorio _publicacionRepositorio;
        private readonly IUsuarioRepositorio _usuarioRepositorio;
        private readonly IMultimediaServicio _multimediaServicio;
        public PublicacionServicio(IPublicacionRepositorio publicacionRepositorio, IUsuarioRepositorio usuarioRepositorio, 
            IMultimediaServicio multimediaServicio)
        {
            _publicacionRepositorio = publicacionRepositorio;
            _usuarioRepositorio = usuarioRepositorio;
            _multimediaServicio = multimediaServicio;
        }

        public List<Publicacion> ObtenerPublicaciones()
        {
            return _publicacionRepositorio.ObtenerTodasPublicaciones();
        }
        public void AgregarPublicacion(Publicacion publicacion)
        {
            publicacion.EstaOculto = false;
            _publicacionRepositorio.GuardarPublicacion(publicacion);
        }
        public List<Publicacion> ObtenerPublicacionPorCategoria(CategoriaEnum categoria)
        {
            return _publicacionRepositorio.ObtenerPublicacionPorCategoria(categoria);
        }

        public async Task<int> Crear(CrearPublicacionDto dto)
        {
            var existeAutor = await _usuarioRepositorio.ExisteUsuario(dto.AutorId);

            if(!existeAutor)
                throw new KeyNotFoundException(
                    "El usuario indicado no existe.");

            var urlMultimedia = await _multimediaServicio.SubirAsync(
                dto.Archivo,
                dto.NombreArchivo,
                dto.UrlArchivo,
                dto.TipoMultimedia);

            var lugar = new Lugar
            {
                Nombre = dto.NombreLugar,

                Coordenadas = new Point(
                    dto.Longitud,
                    dto.Latitud)
                {
                    SRID = 4326
                }
            };

            var publicacion = new Publicacion
            {
                Titulo = dto.Titulo,
                Descripcion = dto.Descripcion,
                Fecha = dto.Fecha,

                Categoria = dto.Categoria,

                UrlMultimedia = urlMultimedia,
                TipoMultimedia = dto.TipoMultimedia,

                Lugar = lugar,

                UsuarioId = dto.AutorId,

                FechaCreación = DateTime.UtcNow,
                EstaOculto = false,
                MotivoOculto = null
            };

            return await _publicacionRepositorio.Crear(publicacion);
        }

    }
}
