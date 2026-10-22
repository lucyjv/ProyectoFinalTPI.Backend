using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;

namespace ProyectoFinalTPI.Backend.Services;

public static class PrevisualizarReprocesamiento
{
    public static async Task Ejecutar(IConfiguration configuracion, string[] args)
    {
        var indice = Array.IndexOf(args, "--limite");
        var limite = indice < 0 ? 10 : indice + 1 < args.Length && int.TryParse(args[indice + 1], out var valor)
            ? valor : throw new ArgumentException("--limite requiere un entero entre 1 y 25.");
        if (limite is < 1 or > 25) throw new ArgumentException("El límite debe estar entre 1 y 25.");
        var conexion = configuracion.GetConnectionString("PostgresConnection");
        if (string.IsNullOrWhiteSpace(conexion)) throw new InvalidOperationException("Falta configurar la conexión PostgreSQL.");
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(conexion, o => o.UseNetTopologySuite()).Options);
        using var cancelacion = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var vista = await new ReprocesamientoRecuerdosRepositorio(db).Previsualizar(limite, 0,
            args.Contains("--incluir-fallidos"), cancelacion.Token);
        // La CLI no muestra títulos/descripciones ni inicia el host, seeder, worker o migraciones.
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            modo = "soloVistaPrevia", vista.HayMas, vista.SiguienteDespuesDeId,
            recuerdos = vista.Recuerdos.Select(r => new { r.PublicacionId, r.VersionContenido, r.Estado })
        }));
    }
}
