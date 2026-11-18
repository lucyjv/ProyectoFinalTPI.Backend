using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Dtos.Capsulas;
using ProyectoFinalTPI.Backend.Dtos.Publicacion;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public sealed class CapsulaRepositorio(ApplicationDbContext contexto, TimeProvider? reloj = null) : ICapsulaRepositorio
{
    private DateTime Ahora => (reloj ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    // Toda lectura parte de este permiso, incluido el hilo, la portada y el mapa.
    private IQueryable<Capsula> Legibles(int? lectorId) => contexto.Capsulas.AsNoTracking()
        .Where(c => !c.Propietario.EstaEliminado &&
            (c.Visibilidad == VisibilidadCapsula.Publica || c.PropietarioId == lectorId));

    private IQueryable<CapsulaRecuerdo> RecuerdosVisibles() => contexto.CapsulasPublicaciones.AsNoTracking()
        .Where(r => !r.Publicacion.EstaOculto && !r.Publicacion.Usuario.EstaEliminado &&
            r.Publicacion.UsuarioId == r.Capsula.PropietarioId && r.Publicacion.PublicacionOriginalId == null);

    private IQueryable<CapsulaRecuerdo> RecuerdosLegibles(int id, int? lectorId)
    {
        var capsulas = Legibles(lectorId);
        return RecuerdosVisibles().Where(r => r.CapsulaId == id && capsulas.Any(c => c.Id == r.CapsulaId));
    }

    public Task<List<CapsulaDto>> Listar(int propietarioId, bool incluirPrivadas, int? antesId, int cantidad, CancellationToken ct)
    {
        var consulta = Legibles(incluirPrivadas ? propietarioId : null).Where(c => c.PropietarioId == propietarioId);
        if (antesId.HasValue) consulta = consulta.Where(c => c.Id < antesId.Value);
        return Proyectar(consulta.OrderByDescending(c => c.Id).Take(cantidad), incluirPrivadas ? propietarioId : null).ToListAsync(ct);
    }

    public Task<CapsulaDto?> Obtener(int id, int? lectorId, CancellationToken ct) =>
        Proyectar(Legibles(lectorId).Where(c => c.Id == id), lectorId).SingleOrDefaultAsync(ct);

    private IQueryable<CapsulaDto> Proyectar(IQueryable<Capsula> consulta, int? lectorId)
    {
        var visibles = RecuerdosVisibles();
        return consulta.Select(c => new CapsulaDto
        {
            Id = c.Id, PropietarioId = c.PropietarioId, PropietarioUsername = c.Propietario.Username,
            Nombre = c.Nombre, Descripcion = c.Descripcion, Visibilidad = c.Visibilidad,
            FechaCreacion = c.FechaCreacion, FechaActualizacion = c.FechaActualizacion,
            PortadaSubida = c.PortadaRecursoId != null,
            PuedeAdministrar = c.PropietarioId == lectorId,
            CantidadRecuerdos = visibles.Count(r => r.CapsulaId == c.Id),
            FechaDesde = visibles.Where(r => r.CapsulaId == c.Id).Min(r => (DateTime?)r.Publicacion.Fecha),
            FechaHasta = visibles.Where(r => r.CapsulaId == c.Id).Max(r => (DateTime?)r.Publicacion.Fecha),
            PortadaPublicacionId = visibles.Any(r => r.CapsulaId == c.Id && r.PublicacionId == c.PortadaPublicacionId)
                ? c.PortadaPublicacionId : null,
            PortadaAdjuntoId = visibles.Any(r => r.CapsulaId == c.Id && r.PublicacionId == c.PortadaPublicacionId &&
                r.Publicacion.Adjuntos.Any(a => a.Id == c.PortadaAdjuntoId && a.Tipo == MultimediaEnum.Foto))
                ? c.PortadaAdjuntoId : null,
            PortadaUrl = c.PortadaRecursoId != null ? "/api/capsulas/" + c.Id + "/portada" : visibles.Where(r => r.CapsulaId == c.Id && r.PublicacionId == c.PortadaPublicacionId)
                .Select(r => c.PortadaAdjuntoId.HasValue
                    ? r.Publicacion.Adjuntos.Where(a => a.Id == c.PortadaAdjuntoId && a.Tipo == MultimediaEnum.Foto).Select(a => a.Url).FirstOrDefault()
                    : r.Publicacion.Adjuntos.Where(a => a.Tipo == MultimediaEnum.Foto).OrderBy(a => a.Orden).Select(a => a.Url).FirstOrDefault()
                        ?? (r.Publicacion.TipoMultimedia == MultimediaEnum.Foto && r.Publicacion.UrlMultimedia != "" ? r.Publicacion.UrlMultimedia : null))
                .FirstOrDefault()
                ?? visibles.Where(r => r.CapsulaId == c.Id &&
                    (r.Publicacion.Adjuntos.Any(a => a.Tipo == MultimediaEnum.Foto) ||
                        r.Publicacion.TipoMultimedia == MultimediaEnum.Foto && r.Publicacion.UrlMultimedia != ""))
                .OrderBy(r => r.Publicacion.Fecha).ThenBy(r => r.PublicacionId)
                .Select(r => r.Publicacion.Adjuntos.Where(a => a.Tipo == MultimediaEnum.Foto).OrderBy(a => a.Orden).Select(a => a.Url).FirstOrDefault()
                    ?? r.Publicacion.UrlMultimedia).FirstOrDefault()
        });
    }

    public async Task<int> Crear(Capsula capsula, IReadOnlyCollection<int> publicacionesIds, CancellationToken ct)
    {
        await Mutar(capsula.PropietarioId, async () =>
        {
            await ValidarPublicaciones(capsula.PropietarioId, publicacionesIds, ct);
            await ValidarPortada(capsula, publicacionesIds, ct);
            capsula.Recuerdos = publicacionesIds.Select(id => new CapsulaRecuerdo { PublicacionId = id, FechaIncorporacion = Ahora }).ToList();
            contexto.Capsulas.Add(capsula);
        }, ct);
        return capsula.Id;
    }

    public Task<string?> Actualizar(int id, int propietarioId, ActualizarCapsulaDto datos, CancellationToken ct) =>
        ActualizarDatos(id, propietarioId, datos, null, ct);

    public Task<string?> ActualizarConPortada(int id, int propietarioId, ActualizarCapsulaDto datos, string recursoId, CancellationToken ct) =>
        ActualizarDatos(id, propietarioId, datos, recursoId, ct);

    private Task<string?> ActualizarDatos(int id, int propietarioId, ActualizarCapsulaDto datos, string? recursoId, CancellationToken ct) => Mutar<string?>(propietarioId, async () =>
    {
        var capsula = await ObtenerPropia(id, propietarioId, ct);
        var anterior = capsula.PortadaRecursoId;
        capsula.Nombre = datos.Nombre; capsula.Descripcion = datos.Descripcion; capsula.Visibilidad = datos.Visibilidad;
        capsula.PortadaPublicacionId = datos.PortadaPublicacionId; capsula.PortadaAdjuntoId = datos.PortadaAdjuntoId;
        if (recursoId is not null)
        {
            capsula.PortadaRecursoId = recursoId;
            capsula.PortadaPublicacionId = null; capsula.PortadaAdjuntoId = null;
        }
        else if (!datos.ConservarPortadaSubida || datos.PortadaPublicacionId.HasValue)
            capsula.PortadaRecursoId = null;
        var miembros = await contexto.CapsulasPublicaciones.Where(r => r.CapsulaId == id).Select(r => r.PublicacionId).ToListAsync(ct);
        await ValidarPortada(capsula, miembros, ct);
        capsula.FechaActualizacion = Ahora;
        return anterior != capsula.PortadaRecursoId ? anterior : null;
    }, ct);

    public Task<string?> Eliminar(int id, int propietarioId, CancellationToken ct) => Mutar<string?>(propietarioId, async () =>
    {
        var capsula = await ObtenerPropia(id, propietarioId, ct);
        contexto.Capsulas.Remove(capsula);
        return capsula.PortadaRecursoId;
    }, ct);

    public Task<string?> ObtenerRecursoPortada(int id, int? lectorId, CancellationToken ct) =>
        Legibles(lectorId).Where(c => c.Id == id).Select(c => c.PortadaRecursoId).SingleOrDefaultAsync(ct);

    public Task AgregarRecuerdos(int id, int propietarioId, IReadOnlyCollection<int> publicacionesIds, CancellationToken ct) => Mutar(propietarioId, async () =>
    {
        var capsula = await ObtenerPropia(id, propietarioId, ct);
        await ValidarPublicaciones(propietarioId, publicacionesIds, ct);
        var existentes = await contexto.CapsulasPublicaciones.Where(r => r.CapsulaId == id && publicacionesIds.Contains(r.PublicacionId))
            .Select(r => r.PublicacionId).ToListAsync(ct);
        var nuevos = publicacionesIds.Except(existentes).ToArray();
        foreach (var publicacionId in nuevos)
            contexto.CapsulasPublicaciones.Add(new CapsulaRecuerdo { CapsulaId = id, PublicacionId = publicacionId, FechaIncorporacion = Ahora });
        if (nuevos.Length > 0) capsula.FechaActualizacion = Ahora;
    }, ct);

    public Task QuitarRecuerdo(int id, int propietarioId, int publicacionId, CancellationToken ct) => Mutar(propietarioId, async () =>
    {
        var capsula = await ObtenerPropia(id, propietarioId, ct);
        var relacion = await contexto.CapsulasPublicaciones.SingleOrDefaultAsync(r => r.CapsulaId == id && r.PublicacionId == publicacionId, ct);
        if (relacion is null) return;
        contexto.CapsulasPublicaciones.Remove(relacion);
        if (capsula.PortadaPublicacionId == publicacionId)
        {
            capsula.PortadaPublicacionId = null; capsula.PortadaAdjuntoId = null;
        }
        capsula.FechaActualizacion = Ahora;
    }, ct);

    private async Task Mutar(int propietarioId, Func<Task> accion, CancellationToken ct)
        => await Mutar<object?>(propietarioId, async () => { await accion(); return null; }, ct);

    private async Task<T> Mutar<T>(int propietarioId, Func<Task<T>> accion, CancellationToken ct)
    {
        await using var transaccion = contexto.Database.IsRelational() ? await contexto.Database.BeginTransactionAsync(ct) : null;
        await PersistenciaCapsulas.BloquearPropietario(contexto, propietarioId, ct);
        var resultado = await accion();
        await contexto.SaveChangesAsync(ct);
        if (transaccion is not null) await transaccion.CommitAsync(ct);
        return resultado;
    }

    private async Task<Capsula> ObtenerPropia(int id, int propietarioId, CancellationToken ct) =>
        await contexto.Capsulas.SingleOrDefaultAsync(c => c.Id == id && c.PropietarioId == propietarioId, ct)
            ?? throw new KeyNotFoundException("La cápsula no existe o no está disponible.");

    private async Task ValidarPublicaciones(int propietarioId, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        var cantidad = await contexto.Publicaciones.CountAsync(p => ids.Contains(p.Id) && p.UsuarioId == propietarioId &&
            !p.EstaOculto && p.PublicacionOriginalId == null && !p.Usuario.EstaEliminado, ct);
        if (cantidad != ids.Count) throw new ArgumentException("Solo podés agregar recuerdos originales propios y visibles.");
    }

    private async Task ValidarPortada(Capsula capsula, IReadOnlyCollection<int> miembros, CancellationToken ct)
    {
        if (!capsula.PortadaPublicacionId.HasValue)
        {
            if (capsula.PortadaAdjuntoId.HasValue) throw new ArgumentException("La portada requiere indicar su publicación.");
            return;
        }
        if (!miembros.Contains(capsula.PortadaPublicacionId.Value)) throw new ArgumentException("La portada debe pertenecer a un recuerdo de la cápsula.");
        var publicacion = contexto.Publicaciones.Where(p => p.Id == capsula.PortadaPublicacionId &&
            p.UsuarioId == capsula.PropietarioId && !p.EstaOculto && !p.Usuario.EstaEliminado && p.PublicacionOriginalId == null);
        var valida = capsula.PortadaAdjuntoId.HasValue
            ? await publicacion.AnyAsync(p => p.Adjuntos.Any(a => a.Id == capsula.PortadaAdjuntoId && a.Tipo == MultimediaEnum.Foto), ct)
            : await publicacion.AnyAsync(p => p.Adjuntos.Any(a => a.Tipo == MultimediaEnum.Foto) ||
                p.TipoMultimedia == MultimediaEnum.Foto && p.UrlMultimedia != "", ct);
        if (!valida) throw new ArgumentException("Elegí una foto disponible del recuerdo seleccionado.");
    }

    public async Task<List<RecuerdoDisponibleCapsulaDto>> ObtenerDisponibles(int propietarioId, int? capsulaId,
        DateTime? antesFecha, int? antesId, int cantidad, CancellationToken ct)
    {
        var consulta = contexto.Publicaciones.AsNoTracking().Where(p => p.UsuarioId == propietarioId &&
            !p.Usuario.EstaEliminado && !p.EstaOculto && p.PublicacionOriginalId == null);
        if (capsulaId.HasValue) consulta = consulta.Where(p => contexto.Capsulas.Any(c => c.Id == capsulaId && c.PropietarioId == propietarioId));
        if (antesFecha.HasValue) consulta = consulta.Where(p => p.Fecha < antesFecha || p.Fecha == antesFecha && p.Id < antesId);
        return await consulta.OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id).Take(cantidad)
            .Select(p => new RecuerdoDisponibleCapsulaDto
            {
                Id = p.Id, Titulo = p.Titulo, Fecha = p.Fecha,
                PortadaUrl = p.Adjuntos.Where(a => a.Tipo == MultimediaEnum.Foto).OrderBy(a => a.Orden).Select(a => a.Url).FirstOrDefault()
                    ?? (p.TipoMultimedia == MultimediaEnum.Foto && p.UrlMultimedia != "" ? p.UrlMultimedia : null),
                YaAgregado = capsulaId.HasValue && contexto.CapsulasPublicaciones.Any(r => r.CapsulaId == capsulaId && r.PublicacionId == p.Id)
            }).ToListAsync(ct);
    }

    public Task<List<ReferenciaRecuerdoCapsulaDto>> ObtenerReferencias(int id, int? lectorId,
        DateTime? desdeFecha, int? desdeId, int cantidad, CancellationToken ct)
    {
        var consulta = RecuerdosLegibles(id, lectorId);
        if (desdeFecha.HasValue) consulta = consulta.Where(r => r.Publicacion.Fecha > desdeFecha || r.Publicacion.Fecha == desdeFecha && r.PublicacionId > desdeId);
        return consulta.OrderBy(r => r.Publicacion.Fecha).ThenBy(r => r.PublicacionId).Take(cantidad)
            .Select(r => new ReferenciaRecuerdoCapsulaDto(r.PublicacionId, r.Publicacion.Fecha)).ToListAsync(ct);
    }

    public Task<List<PublicacionPinDto>> ObtenerPines(int id, int? lectorId, ConsultaMapaCapsulaDto filtro,
        int? despuesId, int cantidad, CancellationToken ct)
    {
        var consulta = RecuerdosLegibles(id, lectorId);
        if (despuesId.HasValue) consulta = consulta.Where(r => r.PublicacionId > despuesId);
        if (filtro.MinLatitud.HasValue) consulta = consulta.Where(r =>
            r.Publicacion.Lugar.Coordenadas.Y >= filtro.MinLatitud && r.Publicacion.Lugar.Coordenadas.Y <= filtro.MaxLatitud &&
            r.Publicacion.Lugar.Coordenadas.X >= filtro.MinLongitud && r.Publicacion.Lugar.Coordenadas.X <= filtro.MaxLongitud);
        return consulta.OrderBy(r => r.PublicacionId).Take(cantidad).Select(r => new PublicacionPinDto
        {
            Id = r.PublicacionId, Titulo = r.Publicacion.Titulo, Año = r.Publicacion.Fecha.Year,
            Categoria = r.Publicacion.Categoria, Latitud = r.Publicacion.Lugar.Coordenadas.Y, Longitud = r.Publicacion.Lugar.Coordenadas.X
        }).ToListAsync(ct);
    }

    public async Task<LimitesMapaCapsulaDto?> ObtenerLimites(int id, int? lectorId, CancellationToken ct) =>
        await RecuerdosLegibles(id, lectorId).GroupBy(r => r.CapsulaId).Select(g => new LimitesMapaCapsulaDto(
            g.Min(r => r.Publicacion.Lugar.Coordenadas.Y), g.Max(r => r.Publicacion.Lugar.Coordenadas.Y),
            g.Min(r => r.Publicacion.Lugar.Coordenadas.X), g.Max(r => r.Publicacion.Lugar.Coordenadas.X))).SingleOrDefaultAsync(ct);

    public async Task ValidarParaPublicar(int propietarioId, IReadOnlyCollection<int> capsulasIds, CancellationToken ct)
    {
        if (capsulasIds.Count == 0) return;
        var cantidad = await contexto.Capsulas.CountAsync(c => capsulasIds.Contains(c.Id) && c.PropietarioId == propietarioId && !c.Propietario.EstaEliminado, ct);
        if (cantidad != capsulasIds.Count) throw new ArgumentException("Todas las cápsulas seleccionadas deben existir y pertenecerte.");
    }
}
