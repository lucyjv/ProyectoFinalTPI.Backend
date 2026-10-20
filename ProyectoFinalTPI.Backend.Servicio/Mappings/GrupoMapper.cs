using ProyectoFinalTPI.Backend.Dtos.Grupo;
using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Servicio.Mappings;

internal static class GrupoMapper
{
    public static GrupoResumenDto ToDto(this Grupo grupo, int? usuarioActualId)
    {
        return new GrupoResumenDto
        {
            Id = grupo.Id,
            Nombre = grupo.Nombre,
            Descripcion = grupo.Descripcion,
            Decada = grupo.Decada,
            FechaCreacion = grupo.FechaCreacion,
            Creador = new AutorResumenDto
            {
                Id = grupo.CreadorId,
                Username = grupo.Creador.Username
            },
            CantidadIntegrantes = grupo.Integrantes.Count,
            EsIntegrante = usuarioActualId.HasValue &&
                grupo.Integrantes.Any(integrante => integrante.UsuarioId == usuarioActualId.Value),
            EsCreador = usuarioActualId == grupo.CreadorId
        };
    }

    public static GrupoIntegranteDto ToDto(this GrupoIntegrante integrante, int creadorId)
    {
        return new GrupoIntegranteDto
        {
            Usuario = new AutorResumenDto
            {
                Id = integrante.UsuarioId,
                Username = integrante.Usuario.Username
            },
            FechaUnion = integrante.FechaUnion,
            EsCreador = integrante.UsuarioId == creadorId
        };
    }
}
