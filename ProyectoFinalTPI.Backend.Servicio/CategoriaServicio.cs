using ProyectoFinalTPI.Backend.Dtos.Usuario;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.Categorias;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;

namespace ProyectoFinalTPI.Backend.Servicio;

public sealed class CategoriaServicio(
    ICategoriaRepositorio progreso) : ICategoriaServicio
{
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

    #endregion

    #region Métodos privados y validaciones

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
            0,
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