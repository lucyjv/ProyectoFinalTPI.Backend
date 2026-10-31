using ProyectoFinalTPI.Backend.Dtos.Auth;
using ProyectoFinalTPI.Backend.Entidades;

namespace ProyectoFinalTPI.Backend.Mappings;

public static class UsuarioMappings
{
    public static UsuarioAutenticadoDto ToUsuarioAutenticadoDto(this Usuario usuario)
    {
        return new UsuarioAutenticadoDto
        {
            UsuarioId = usuario.Id,
            Username = usuario.Username,
            FotoPerfilUrl = usuario.FotoPerfilUrl,
            Email = usuario.Email,
            EsAdmin = usuario.EsAdmin
        };
    }
}
