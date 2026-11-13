using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.Categorias;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class CategoriaServicio(ICategoriaRepositorio progreso, TimeProvider? reloj = null) : ICategoriaServicio
{
    private readonly TimeProvider reloj = reloj ?? TimeProvider.System;
    private const int PuntosParaAedo = 30;

    private static readonly IReadOnlyList<CategoriaDefinicion> Categorias =
        CrearCatalogo();

    #region Operaciones públicas

    public IReadOnlyList<CategoriaCuentaDto> ObtenerCatalogo() =>
        Categorias.Select(Mapear).ToArray();

    public CategoriaCuentaDto Obtener(CategoriaCuenta categoria) =>
        Mapear(BuscarDefinicion(categoria));

    public CategoriaCuentaDto ObtenerParaUsuario(Usuario usuario)
    {
        ArgumentNullException.ThrowIfNull(usuario);
        return Obtener(ResolverCategoria(usuario));
    }

    public bool TienePermiso(
        CategoriaCuenta categoria, PermisoCuenta permiso) =>
        BuscarDefinicion(categoria).Permisos.Contains(permiso);

    public Task ActualizarPorRecuerdo(
        Publicacion? publicacion,
        int puntosRecibidos,
        CancellationToken ct = default)
    {
        if (publicacion is null ||
            publicacion.EstaOculto ||
            publicacion.PublicacionOriginalId.HasValue)
            return Task.CompletedTask;

        var destino = ResolverAscenso(puntosRecibidos);

        return progreso.PromoverHasta(
            publicacion.UsuarioId, destino, ct);
    }

    public async Task<EstadoCategoriaDto> ObtenerEstado(int usuarioId, CancellationToken ct = default)
    {
        var usuario = await progreso.ObtenerUsuario(usuarioId, ct)
            ?? throw new KeyNotFoundException("El usuario no existe.");
        var codigo = ResolverCategoria(usuario);
        // Compatibilidad con cuentas que publicaron antes de persistir los ascensos.
        if (codigo == CategoriaCuenta.Errante && usuario is Usuario_Personal
            && await progreso.TieneRecuerdos(usuarioId, ct)) codigo = CategoriaCuenta.Nostalgico;
        var categoria = Obtener(codigo);
        var semana = ObtenerSemana();
        var consumo = await progreso.ObtenerConsumo(usuarioId, semana, ct);
        var limite = categoria.PublicacionesSemanales;
        return new(categoria, consumo, limite.HasValue ? Math.Max(0, limite.Value - consumo) : null,
            semana, semana.AddDays(7), TienePermiso(codigo, PermisoCuenta.AdjuntarVariasFotos) ? 4 : 1);
    }

    public async Task ExigirPermiso(int usuarioId, PermisoCuenta permiso, CancellationToken ct = default)
    {
        var estado = await ObtenerEstado(usuarioId, ct);
        if (!estado.Categoria.Permisos.Contains(permiso.ToString()))
            throw new UnauthorizedAccessException($"La categoría {estado.Categoria.Nombre} no permite esta acción.");
    }

    public async Task ValidarCreacion(int usuarioId, int cantidadAdjuntos, bool tieneMusica, CancellationToken ct = default)
    {
        var estado = await ObtenerEstado(usuarioId, ct);
        if (!estado.Categoria.PermitePrimerRecuerdo && !estado.Categoria.Permisos.Contains(nameof(PermisoCuenta.CrearRecuerdo)))
            throw new UnauthorizedAccessException("Tu categoría no permite crear recuerdos.");
        if (cantidadAdjuntos < 0 || cantidadAdjuntos > 4)
            throw new ArgumentException("Una publicación admite hasta 4 adjuntos.");
        if (cantidadAdjuntos > estado.MaximoAdjuntos)
            throw new UnauthorizedAccessException("Tu categoría permite un solo adjunto por publicación.");
        if (tieneMusica && !estado.Categoria.Permisos.Contains(nameof(PermisoCuenta.AgregarMusicaAPublicacion)))
            throw new UnauthorizedAccessException("La música en publicaciones está disponible desde Aedo.");
        ValidarCupo(estado);
    }

    public async Task<int> CrearRecuerdo(Publicacion publicacion, CancellationToken ct = default)
    {
        if (publicacion.PublicacionOriginalId.HasValue) throw new ArgumentException("Esta operación crea recuerdos propios.");
        var estado = await ObtenerEstado(publicacion.UsuarioId, ct);
        ValidarCupo(estado);
        publicacion.FechaCreación = reloj.GetUtcNow().UtcDateTime;
        var limite = estado.Categoria.PublicacionesSemanales;
        var id = await progreso.CrearConCupo(publicacion, estado.SemanaInicio, limite, ct);
        if (!id.HasValue) throw CrearErrorCupo(estado);
        await ActualizarPorRecuerdo(publicacion, 0, ct);
        return id.Value;
    }

    public double ObtenerBonificacion(ReputacionEnum? reputacion, bool marca, double alta, double muyAlta) =>
        marca || reputacion == ReputacionEnum.Mnnemónide ? muyAlta
        : reputacion == ReputacionEnum.Aedo ? alta : 0;

    #endregion

    #region Métodos privados y validaciones

    private DateOnly ObtenerSemana()
    {
        var fecha = TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"));
        return DateOnly.FromDateTime(fecha.DateTime).AddDays(-(int)fecha.DayOfWeek);
    }

    private static void ValidarCupo(EstadoCategoriaDto estado)
    {
        if (estado.PublicacionesRestantes == 0) throw CrearErrorCupo(estado);
    }

    private static CupoPublicacionesAgotadoException CrearErrorCupo(EstadoCategoriaDto estado) =>
        new($"Alcanzaste el límite semanal de publicaciones. Tu cupo se renueva el {estado.ReinicioSemana:dd/MM/yyyy} a las 00:00 (Argentina).");

    private static ReputacionEnum ResolverAscenso(int puntosRecibidos)
    {
        if (puntosRecibidos < 0)
            throw new ArgumentOutOfRangeException(
                nameof(puntosRecibidos));

        return puntosRecibidos >= PuntosParaAedo
            ? ReputacionEnum.Aedo
            : ReputacionEnum.Nostalgico;
    }

    private static CategoriaCuenta ResolverCategoria(Usuario usuario) =>
        usuario switch
        {
            Usuario_Marca => CategoriaCuenta.Marca,
            Usuario_Personal personal =>
                DesdeReputacion(personal.Reputacion),
            _ => CategoriaCuenta.Errante
        };

    private static CategoriaCuenta DesdeReputacion(
        ReputacionEnum reputacion) =>
        reputacion switch
        {
            ReputacionEnum.Nostalgico => CategoriaCuenta.Nostalgico,
            ReputacionEnum.Aedo => CategoriaCuenta.Aedo,
            ReputacionEnum.Mnnemónide => CategoriaCuenta.Mnemosine,
            _ => CategoriaCuenta.Errante
        };

    private static CategoriaDefinicion BuscarDefinicion(
        CategoriaCuenta categoria) =>
        Categorias.FirstOrDefault(
            definicion => definicion.Codigo == categoria)
            ?? throw new ArgumentOutOfRangeException(
                nameof(categoria), "La categoría no existe.");

    private static IReadOnlyList<CategoriaDefinicion> CrearCatalogo()
    {
        var errante = CrearDefinicion(
            CategoriaCuenta.Errante,
            "Errante",
            5,
            PrioridadVisibilidad.Normal,
            PermisoCuenta.DarLike,
            PermisoCuenta.GuardarPublicacion,
            PermisoCuenta.SerEtiquetado,
            PermisoCuenta.SeguirPerfil);

        var nostalgico = Heredar(
            errante,
            CategoriaCuenta.Nostalgico,
            "Nostálgico",
            5,
            PrioridadVisibilidad.Normal,
            PermisoCuenta.CrearRecuerdo,
            PermisoCuenta.Comentar,
            PermisoCuenta.Compartir,
            PermisoCuenta.EtiquetarUsuario,
            PermisoCuenta.PuntuarPublicacion,
            PermisoCuenta.VotarCancion);

        var aedo = Heredar(
            nostalgico,
            CategoriaCuenta.Aedo,
            "Aedo",
            7,
            PrioridadVisibilidad.Alta,
            PermisoCuenta.AgregarMusicaAPublicacion);

        var mnemosine = Heredar(
            aedo,
            CategoriaCuenta.Mnemosine,
            "Mnemosine",
            null,
            PrioridadVisibilidad.MuyAlta,
            PermisoCuenta.SugerirMusica,
            PermisoCuenta.AdjuntarVariasFotos);

        var marca = Heredar(
            mnemosine,
            CategoriaCuenta.Marca,
            "Marca",
            null,
            PrioridadVisibilidad.MuyAlta,
            PermisoCuenta.UsarPinEspecial,
            PermisoCuenta.PublicitarPublicacion,
            PermisoCuenta.VerificacionDeMarca,
            PermisoCuenta.UsarPerfilFijo);

        return Array.AsReadOnly(new[]
        {
            errante, nostalgico, aedo, mnemosine, marca
        });
    }

    private static CategoriaDefinicion Heredar(
        CategoriaDefinicion anterior,
        CategoriaCuenta codigo,
        string nombre,
        int? publicacionesSemanales,
        PrioridadVisibilidad visibilidad,
        params PermisoCuenta[] adicionales) =>
        CrearDefinicion(
            codigo,
            nombre,
            publicacionesSemanales,
            visibilidad,
            anterior.Permisos
                .Concat(adicionales)
                .Distinct()
                .ToArray());

    private static CategoriaDefinicion CrearDefinicion(
        CategoriaCuenta codigo,
        string nombre,
        int? publicacionesSemanales,
        PrioridadVisibilidad visibilidad,
        params PermisoCuenta[] permisos) =>
        new()
        {
            Codigo = codigo,
            Nombre = nombre,
            PublicacionesSemanales = publicacionesSemanales,
            Visibilidad = visibilidad,
            Permisos = Array.AsReadOnly(permisos),

            PublicacionesIlimitadas =
                permisos.Contains(PermisoCuenta.CrearRecuerdo) &&
                publicacionesSemanales is null,

            PermitePrimerRecuerdo =
                codigo == CategoriaCuenta.Errante
        };

    private static CategoriaCuentaDto Mapear(
        CategoriaDefinicion categoria) =>
        new(
            categoria.Codigo.ToString(),
            categoria.Nombre,
            categoria.PublicacionesSemanales,
            categoria.PublicacionesIlimitadas,
            categoria.PermitePrimerRecuerdo,
            categoria.Visibilidad.ToString(),
            categoria.Permisos
                .Select(permiso => permiso.ToString())
                .ToArray());

    #endregion
}