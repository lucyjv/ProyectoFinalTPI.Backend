using NetTopologySuite.Geometries;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;

namespace ProyectoFinalTPI.Backend.Mappings;

public static class PublicacionMappings
{
    public static Point? ToUbicacionGps(this CrearPublicacionRequest request)
    {
        return request.GpsLatitud.HasValue && request.GpsLongitud.HasValue
            ? new Point(request.GpsLongitud.Value, request.GpsLatitud.Value) { SRID = 4326 }
            : null;
    }

    public static Publicacion ToPublicacion(this CrearPublicacionRequest request, int autorId)
    {
        return new Publicacion
        {
            Titulo = request.Titulo,
            Descripcion = request.Descripcion,
            Fecha = request.Fecha,
            Categoria = request.Categoria,
            Lugar = new Lugar
            {
                Nombre = request.NombreLugar,
                Coordenadas = new Point(request.Longitud, request.Latitud) { SRID = 4326 }
            },
            TipoMultimedia = request.TipoMultimedia,
            UrlMultimedia = request.UrlArchivo ?? string.Empty,
            UsuarioId = autorId
        };
    }
}
