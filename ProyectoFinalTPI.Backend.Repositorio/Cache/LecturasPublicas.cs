using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio.Cache;

public sealed class PerfilPublicoConsulta(IOrigenLecturasPublicas origen, ICacheLecturasPublicas cache) : IPerfilPublicoConsulta
{
    public async Task<Usuario?> Obtener(int id, CancellationToken cancellationToken = default)
    {
        var datos = await cache.Obtener($"perfil:{id}", async () =>
        {
            var usuario = await origen.ObtenerUsuario(id, cancellationToken);
            if (usuario is null) return null;
            return usuario switch
            {
                Usuario_Personal p => new DatosPerfilPublico(p.Id, p.Username, p.FotoPerfilUrl, "personal", p.Nombre, p.Apellido, p.Reputacion, p.PuntosNostalgia),
                Usuario_Marca m => new DatosPerfilPublico(m.Id, m.Username, m.FotoPerfilUrl, "marca", m.NombreEmpresa, null, default, m.PuntosNostalgia),
                _ => new DatosPerfilPublico(usuario.Id, usuario.Username, usuario.FotoPerfilUrl, "usuario", null, null, default, 0)
            };
        }, cancellationToken);
        if (datos is null) return null;
        // Sólo para lectura del perfil: nunca retorna credenciales ni entidades seguidas por EF.
        Usuario resultado = datos.Tipo switch
        {
            "personal" => new Usuario_Personal { Nombre = datos.Nombre!, Apellido = datos.Apellido!, Reputacion = datos.Reputacion, PuntosNostalgia = datos.Puntos },
            "marca" => new Usuario_Marca { NombreEmpresa = datos.Nombre!, PuntosNostalgia = datos.Puntos },
            _ => new Usuario()
        };
        resultado.Id = datos.Id;
        resultado.Username = datos.Username;
        resultado.FotoPerfilUrl = datos.Foto;
        return resultado;
    }
}

public sealed class ResumenPublicacionConsulta(IOrigenLecturasPublicas origen, IPublicacionRepositorio repositorio, ICacheLecturasPublicas cache) : IResumenPublicacionConsulta
{
    public async Task<PublicacionResumenDto?> Obtener(int id, CancellationToken cancellationToken = default)
    {
        // La visibilidad y existencia no dependen del TTL ni de Redis.
        if (!await origen.EsPublicacionVisible(id, cancellationToken)) return null;
        return await cache.Obtener($"resumen:{id}", () => repositorio.ObtenerResumenPorId(id, cancellationToken), cancellationToken);
    }
}
