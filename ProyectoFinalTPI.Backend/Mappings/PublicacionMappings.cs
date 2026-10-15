using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Requests.Publicaciones;

namespace ProyectoFinalTPI.Backend.Mappings;

public static class PublicacionMappings
{
    public static CompartirPublicacionDto ToCompartirPublicacionDto(
        this CompartirPublicacionRequest request, int autorId)
    {
        return new CompartirPublicacionDto
        {
            PublicacionOriginalId = request.PublicacionOriginalId,
            AutorId = autorId,
            Descripcion = request.Descripcion ?? string.Empty
        };
    }

    // El controlador administra la apertura y el cierre del stream.
    public static CrearPublicacionDto ToCrearPublicacionDto(
        this CrearPublicacionRequest request, int autorId, Stream? archivo)
    {
        return new CrearPublicacionDto
        {
            Titulo = request.Titulo,
            Descripcion = request.Descripcion,
            Fecha = request.Fecha,
            Categoria = request.Categoria,
            NombreLugar = request.NombreLugar,
            Latitud = request.Latitud,
            Longitud = request.Longitud,
            TipoMultimedia = request.TipoMultimedia,
            Archivo = archivo,
            NombreArchivo = request.Archivo?.FileName,
            UrlArchivo = request.UrlArchivo,
            AutorId = autorId
        };
    }
}
