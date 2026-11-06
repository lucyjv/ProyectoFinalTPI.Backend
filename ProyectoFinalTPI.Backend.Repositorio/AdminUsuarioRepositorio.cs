using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Admin;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public class AdminUsuarioRepositorio(ApplicationDbContext context) : IAdminUsuarioRepositorio
{
    public async Task<PaginaAdminDto<UsuarioAdminDto>> Listar(
        FiltroUsuariosAdminDto filtro, CancellationToken cancellationToken = default)
    {
        var consulta = AplicarFiltros(context.Usuarios.AsNoTracking(), filtro);
        var total = await consulta.CountAsync(cancellationToken);
        var usuarios = await consulta.OrderBy(u => u.Username)
            .Skip((filtro.Pagina - 1) * filtro.Tamanio).Take(filtro.Tamanio).ToListAsync(cancellationToken);
        return new()
        {
            Items = usuarios.Select(Mapear).ToList(), Pagina = filtro.Pagina,
            Tamanio = filtro.Tamanio, TotalItems = total
        };
    }

    public Task<Usuario?> Obtener(int id, CancellationToken cancellationToken = default) =>
        context.Usuarios.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task Guardar(CancellationToken cancellationToken = default) => context.SaveChangesAsync(cancellationToken);

    private static IQueryable<Usuario> AplicarFiltros(IQueryable<Usuario> consulta, FiltroUsuariosAdminDto filtro)
    {
        var ahora = DateTime.UtcNow;
        if (filtro.EsAdmin.HasValue) consulta = consulta.Where(u => u.EsAdmin == filtro.EsAdmin);
        if (filtro.Eliminado.HasValue) consulta = consulta.Where(u => u.EstaEliminado == filtro.Eliminado);
        if (filtro.Suspendido.HasValue) consulta = consulta.Where(u => (u is Usuario_Interactivo && ((Usuario_Interactivo)u).SuspendidoHasta > ahora) == filtro.Suspendido);
        if (filtro.Tipo.HasValue) consulta = AplicarTipo(consulta, filtro.Tipo.Value);
        if (!string.IsNullOrWhiteSpace(filtro.Texto)) consulta = AplicarTexto(consulta, filtro.Texto);
        return consulta;
    }

    private static IQueryable<Usuario> AplicarTipo(IQueryable<Usuario> consulta, TipoUsuarioAdmin tipo) => tipo switch
    {
        TipoUsuarioAdmin.Moderador => consulta.Where(u => u is Moderador),
        TipoUsuarioAdmin.Marca => consulta.Where(u => u is Usuario_Marca),
        TipoUsuarioAdmin.Personal => consulta.Where(u => u is Usuario_Personal && !(u is Moderador)),
        _ => consulta.Where(u => !(u is Usuario_Interactivo))
    };

    private static IQueryable<Usuario> AplicarTexto(IQueryable<Usuario> consulta, string valor)
    {
        var texto = valor.Trim().ToLower();
        return consulta.Where(u => u.Username.ToLower().Contains(texto) || u.Email.ToLower().Contains(texto) ||
            (u is Usuario_Personal && (((Usuario_Personal)u).Nombre.ToLower().Contains(texto) ||
                ((Usuario_Personal)u).Apellido.ToLower().Contains(texto))) ||
            (u is Usuario_Marca && ((Usuario_Marca)u).NombreEmpresa.ToLower().Contains(texto)));
    }

    private static UsuarioAdminDto Mapear(Usuario usuario)
    {
        var personal = usuario as Usuario_Personal;
        return new()
        {
            Id = usuario.Id, Username = usuario.Username, Email = usuario.Email,
            Nombre = personal?.Nombre ?? (usuario as Usuario_Marca)?.NombreEmpresa,
            Apellido = personal?.Apellido, EsAdmin = usuario.EsAdmin,
            EstaEliminado = usuario.EstaEliminado,
            Tipo = usuario switch
            {
                Moderador => TipoUsuarioAdmin.Moderador,
                Usuario_Marca => TipoUsuarioAdmin.Marca,
                Usuario_Personal => TipoUsuarioAdmin.Personal,
                _ => TipoUsuarioAdmin.Otro
            },
            SuspendidoHasta = (usuario as Usuario_Interactivo)?.SuspendidoHasta,
            FechaCreacion = usuario.FechaCreacion
        };
    }
}
