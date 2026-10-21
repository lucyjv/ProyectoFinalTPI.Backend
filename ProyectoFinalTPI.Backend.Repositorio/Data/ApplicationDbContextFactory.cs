using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace ProyectoFinalTPI.Backend.Repositorio.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .AddUserSecrets(typeof(ApplicationDbContextFactory).Assembly, optional: true)
                .AddEnvironmentVariables()
                .Build();

            var connectionString = configuration.GetConnectionString("PostgresConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "No se configuró ConnectionStrings:PostgresConnection. " +
                    "Configurala en User Secrets o en la variable de entorno " +
                    "ConnectionStrings__PostgresConnection antes de ejecutar EF Core.");
            }

            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            optionsBuilder.UseNpgsql(
                connectionString,
                x => x.UseNetTopologySuite()
            );

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
