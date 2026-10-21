using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Requests.Grupos;
using NetTopologySuite.Geometries;

namespace ProyectoFinalTPI.Backend.Mappings;

public static class GrupoMappings
{
    public static Grupo ToGrupo(this CrearGrupoRequest request, int creadorId)
    {
        return new Grupo
        {
            Nombre = request.Nombre.Trim(),
            Descripcion = request.Descripcion.Trim(),
            Decada = request.Decada,
            CreadorId = creadorId
        };
    }

    public static Grupo ToGrupo(this ActualizarGrupoRequest request)
    {
        return new Grupo
        {
            Nombre = request.Nombre.Trim(),
            Descripcion = request.Descripcion.Trim(),
            Decada = request.Decada
        };
    }

    public static Publicacion ToPublicacion(
        this CrearRecuerdoGrupoRequest request, int grupoId, int autorId)
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
            UsuarioId = autorId,
            GrupoId = grupoId
        };
    }
}
