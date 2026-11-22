using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Entidades.ValueObjects;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Repositorio;

public sealed class RankingMusicaRepositorio(
    ApplicationDbContext contexto, ConsumoDiarioPuntosNostalgiaRepositorio consumo, TimeProvider reloj)
    : IRankingMusicaRepositorio
{
    private DbSet<RankingMusica> Rondas => contexto.Set<RankingMusica>();
    private DbSet<CandidaturaRankingMusica> Candidaturas => contexto.Set<CandidaturaRankingMusica>();
    private DbSet<VotoRankingMusica> Votos => contexto.Set<VotoRankingMusica>();
    private DbSet<ResultadoRankingMusica> Resultados => contexto.Set<ResultadoRankingMusica>();

    public async Task<bool> CrearRondaInicial(DateTimeOffset apertura, DateTimeOffset cierre, CancellationToken ct = default)
    {
        if (cierre <= apertura)
            throw new ArgumentException("El cierre debe ser posterior a la apertura de la ronda.", nameof(cierre));

        return await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO rankings_musica ("Apertura", "Cierre", "Estado")
            SELECT {apertura.ToUniversalTime()}, {cierre.ToUniversalTime()}, {(int)EstadoRankingMusica.Abierto}
            WHERE NOT EXISTS (SELECT 1 FROM rankings_musica)
            ON CONFLICT DO NOTHING
            """, ct) == 1;
    }

    public Task<RankingMusica?> ObtenerRondaAbierta(CancellationToken ct = default) =>
        Rondas.AsNoTracking().SingleOrDefaultAsync(ronda => ronda.Estado == EstadoRankingMusica.Abierto, ct);

    public async Task<IReadOnlyList<CandidaturaRankingMusicaConPuntos>> ListarCandidaturas(
        int rankingMusicaId, CancellationToken ct = default) =>
        await Candidaturas.AsNoTracking()
            .Where(candidatura => candidatura.RankingMusicaId == rankingMusicaId && candidatura.FechaRetiro == null)
            .Select(candidatura => new
            {
                Candidatura = candidatura,
                Puntos = Votos.Where(voto => voto.CandidaturaId == candidatura.Id).Sum(voto => (long)voto.Puntos)
            })
            .OrderBy(fila => fila.Candidatura.Decada)
            .ThenByDescending(fila => fila.Puntos)
            .ThenBy(fila => fila.Candidatura.FechaIngreso)
            .ThenBy(fila => fila.Candidatura.Id)
            .Select(fila => new CandidaturaRankingMusicaConPuntos(fila.Candidatura, fila.Puntos))
            .ToListAsync(ct);

    public async Task<RondaMusicaConResultados?> ObtenerResultados(int? rankingMusicaId = null, CancellationToken ct = default)
    {
        var rondasCerradas = Rondas.AsNoTracking().Where(ronda => ronda.Estado == EstadoRankingMusica.Cerrado);
        if (rankingMusicaId.HasValue)
            rondasCerradas = rondasCerradas.Where(ronda => ronda.Id == rankingMusicaId.Value);

        var ronda = await rondasCerradas.OrderByDescending(ronda => ronda.Cierre).FirstOrDefaultAsync(ct);
        if (ronda is null)
            return null;

        var posiciones = await (
            from resultado in Resultados.AsNoTracking()
            join candidatura in Candidaturas.AsNoTracking() on resultado.CandidaturaId equals candidatura.Id
            where resultado.RankingMusicaId == ronda.Id
            orderby resultado.Decada, resultado.Posicion
            select new PosicionRankingMusica(resultado, candidatura.Titulo, candidatura.Artista)).ToListAsync(ct);
        return new RondaMusicaConResultados(ronda, posiciones);
    }

    public async Task<CandidaturaRankingMusica> AgregarCandidatura(
        int rankingMusicaId, int creadorId, Decada decada, string titulo, string artista, CancellationToken ct = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);
        var ronda = await BloquearRondaParaModificarCandidaturas(rankingMusicaId, ct);
        var ahora = reloj.GetUtcNow();
        ExigirRondaVigente(ronda, ahora);

        var candidatura = new CandidaturaRankingMusica(rankingMusicaId, creadorId, decada, titulo, artista, ahora);
        Candidaturas.Add(candidatura);
        try
        {
            await contexto.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (EsCandidaturaDuplicada(ex.InnerException))
        {
            contexto.Entry(candidatura).State = EntityState.Detached;
            throw ConflictoPorDuplicado();
        }

        await transaccion.CommitAsync(ct);
        return candidatura;
    }

    public async Task<CandidaturaRankingMusica> CorregirCandidatura(
        int candidaturaId, int usuarioId, bool esAdministrador, string titulo, string artista, CancellationToken ct = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);
        var (candidatura, _) = await ObtenerCandidaturaBloqueada(candidaturaId, ct);
        ExigirCreadorOAdministrador(candidatura, usuarioId, esAdministrador);
        if (await Votos.AnyAsync(voto => voto.CandidaturaId == candidaturaId, ct))
            throw new ConflictoRankingMusicaException("La candidatura ya recibió votos y no se puede editar.");

        candidatura.CorregirDatos(titulo, artista);
        try
        {
            await Candidaturas.Where(fila => fila.Id == candidaturaId).ExecuteUpdateAsync(cambios => cambios
                .SetProperty(fila => fila.Titulo, candidatura.Titulo)
                .SetProperty(fila => fila.Artista, candidatura.Artista)
                .SetProperty(fila => fila.TituloNormalizado, candidatura.TituloNormalizado)
                .SetProperty(fila => fila.ArtistaNormalizado, candidatura.ArtistaNormalizado), ct);
        }
        catch (PostgresException ex) when (EsCandidaturaDuplicada(ex))
        {
            throw ConflictoPorDuplicado();
        }

        await transaccion.CommitAsync(ct);
        return candidatura;
    }

    public async Task RetirarCandidatura(int candidaturaId, int usuarioId, bool esAdministrador, CancellationToken ct = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);
        var (candidatura, ahora) = await ObtenerCandidaturaBloqueada(candidaturaId, ct);
        ExigirCreadorOAdministrador(candidatura, usuarioId, esAdministrador);
        await Candidaturas.Where(fila => fila.Id == candidaturaId)
            .ExecuteUpdateAsync(cambios => cambios.SetProperty(fila => fila.FechaRetiro, ahora), ct);
        await transaccion.CommitAsync(ct);
    }

    public async Task<VotoRankingMusicaRegistrado> RegistrarVoto(
        int candidaturaId, int usuarioId, int puntos, int cupoDiario, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(puntos);
        await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);
        await consumo.BloquearUsuario(usuarioId, ct);
        var (_, ahora) = await ObtenerCandidaturaBloqueada(candidaturaId, ct);
        var saldo = await consumo.GastarConUsuarioBloqueado(usuarioId, ahora, puntos, cupoDiario, ct);

        var voto = new VotoRankingMusica { CandidaturaId = candidaturaId, UsuarioId = usuarioId, Puntos = puntos, FechaVoto = ahora };
        Votos.Add(voto);
        await contexto.SaveChangesAsync(ct);
        var total = await Votos.Where(voto => voto.CandidaturaId == candidaturaId).SumAsync(voto => (long)voto.Puntos, ct);
        await transaccion.CommitAsync(ct);
        return new VotoRankingMusicaRegistrado(voto.Id, total, saldo);
    }

    public async Task<bool> CerrarRonda(int rankingMusicaId, DateTimeOffset cierreSiguiente, CancellationToken ct = default)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(ct);
        var ronda = (await Rondas.FromSqlInterpolated($"""
            SELECT * FROM rankings_musica WHERE "Id" = {rankingMusicaId} FOR UPDATE
            """).AsNoTracking().ToListAsync(ct)).SingleOrDefault();
        if (ronda is null || ronda.Estado == EstadoRankingMusica.Cerrado || reloj.GetUtcNow() < ronda.Cierre)
            return false;
        if (cierreSiguiente <= ronda.Cierre)
            throw new ArgumentException("El cierre de la próxima ronda debe ser posterior al cierre de la ronda actual.", nameof(cierreSiguiente));

        var candidaturas = await ListarCandidaturas(ronda.Id, ct);
        var clasificadas = candidaturas.Where(fila => fila.Puntos > 0)
            .GroupBy(fila => fila.Candidatura.Decada)
            .SelectMany(decada => decada.Take(6).Select((fila, indice) => (Fila: fila, Posicion: indice + 1)))
            .ToList();

        await Rondas.Where(fila => fila.Id == ronda.Id)
            .ExecuteUpdateAsync(cambios => cambios.SetProperty(fila => fila.Estado, EstadoRankingMusica.Cerrado), ct);
        var siguiente = new RankingMusica { Apertura = ronda.Cierre, Cierre = cierreSiguiente.ToUniversalTime() };
        Rondas.Add(siguiente);
        await contexto.SaveChangesAsync(ct);

        foreach (var (fila, posicion) in clasificadas)
        {
            var candidatura = fila.Candidatura;
            Resultados.Add(new ResultadoRankingMusica
            {
                RankingMusicaId = ronda.Id,
                CandidaturaId = candidatura.Id,
                Decada = candidatura.Decada,
                Posicion = posicion,
                Puntos = fila.Puntos
            });
            if (posicion >= 4)
                Candidaturas.Add(new CandidaturaRankingMusica(siguiente.Id, candidatura.CreadorId,
                    candidatura.Decada, candidatura.Titulo, candidatura.Artista, siguiente.Apertura));
        }

        await contexto.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return true;
    }

    // Las escrituras toman un bloqueo compartido sobre la ronda. El cierre espera a que terminen.
    private async Task<RankingMusica?> BloquearRondaParaModificarCandidaturas(int rondaId, CancellationToken ct) =>
        (await Rondas.FromSqlInterpolated($"""
            SELECT * FROM rankings_musica WHERE "Id" = {rondaId} FOR SHARE
            """).AsNoTracking().ToListAsync(ct)).SingleOrDefault();

    private async Task<(CandidaturaRankingMusica Candidatura, DateTimeOffset Ahora)> ObtenerCandidaturaBloqueada(
        int candidaturaId, CancellationToken ct)
    {
        var rondaId = await Candidaturas.Where(fila => fila.Id == candidaturaId)
            .Select(fila => (int?)fila.RankingMusicaId).SingleOrDefaultAsync(ct)
            ?? throw CandidaturaNoEncontrada();
        var ronda = await BloquearRondaParaModificarCandidaturas(rondaId, ct);
        // Editar, retirar y registrar un voto sobre la misma candidatura se serializan.
        var candidatura = (await Candidaturas.FromSqlInterpolated($"""
            SELECT * FROM candidaturas_ranking_musica WHERE "Id" = {candidaturaId} FOR UPDATE
            """).AsNoTracking().ToListAsync(ct)).SingleOrDefault();
        if (candidatura is null || candidatura.FechaRetiro.HasValue)
            throw CandidaturaNoEncontrada();

        var ahora = reloj.GetUtcNow();
        ExigirRondaVigente(ronda, ahora);
        return (candidatura, ahora);
    }

    private static void ExigirRondaVigente(RankingMusica? ronda, DateTimeOffset ahora)
    {
        if (ronda is null)
            throw new KeyNotFoundException("La ronda de música no existe.");
        if (ronda.Estado != EstadoRankingMusica.Abierto || ahora < ronda.Apertura || ahora >= ronda.Cierre)
            throw new ConflictoRankingMusicaException("La ronda de música no está abierta a cambios.");
    }

    private static void ExigirCreadorOAdministrador(CandidaturaRankingMusica candidatura, int usuarioId, bool esAdministrador)
    {
        if (!esAdministrador && candidatura.CreadorId != usuarioId)
            throw new UnauthorizedAccessException("Solo el creador o un administrador puede modificar la candidatura.");
    }

    private static bool EsCandidaturaDuplicada(Exception? excepcion) => excepcion is PostgresException
    {
        SqlState: PostgresErrorCodes.UniqueViolation,
        ConstraintName: "IX_candidaturas_ranking_musica_RankingMusicaId_Decada_TituloNo~"
    };

    private static ConflictoRankingMusicaException ConflictoPorDuplicado() =>
        new("La canción ya está en la votación de esa década.");

    private static KeyNotFoundException CandidaturaNoEncontrada() => new("La candidatura no existe o fue retirada.");
}
