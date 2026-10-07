using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ProyectoFinalTPI.Backend.Repositorio.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            optionsBuilder.UseNpgsql(
                "Host=127.0.0.1;Database=Nostalgiar;Username=postgres;Password=123",
                x => x.UseNetTopologySuite()
            );

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
