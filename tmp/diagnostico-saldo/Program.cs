using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;

await using var db = new ApplicationDbContextFactory().CreateDbContext([]);
var connection = db.Database.GetDbConnection();
var destino = new Npgsql.NpgsqlConnectionStringBuilder(connection.ConnectionString);
if (destino.Host is not ("localhost" or "127.0.0.1" or "::1"))
    throw new InvalidOperationException("El diagnóstico requiere una conexión local.");
Console.WriteLine($"Base local: {connection.DataSource}/{connection.Database}");
var categorias = new CategoriaServicio(new CategoriaRepositorio(db));
var consumos = new ConsumoDiarioPuntosNostalgiaRepositorio(db);
var puntos = new CupoPuntosNostalgiaServicio(categorias, consumos, TimeProvider.System);
var ranking = new RankingMusicaServicio(new RankingMusicaRepositorio(db, consumos, TimeProvider.System),
    categorias, puntos, TimeProvider.System, Options.Create(new RankingMusicaOptions()));
var ids = await db.Usuarios.AsNoTracking().Select(u => u.Id).ToListAsync();
var errores = 0;
foreach (var id in ids)
{
    try
    {
        await puntos.ObtenerSaldo(id);
        await ranking.ObtenerActual(id);
    }
    catch (Exception ex)
    {
        errores++;
        Console.WriteLine($"Usuario {id}: {ex}");
        if (errores >= 3) break;
    }
}
Console.WriteLine($"Usuarios locales: {ids.Count}; errores: {errores}");
Environment.ExitCode = errores == 0 ? 0 : 1;
