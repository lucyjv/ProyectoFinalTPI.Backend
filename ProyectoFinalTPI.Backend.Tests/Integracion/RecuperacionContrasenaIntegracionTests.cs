using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using Xunit;

namespace ProyectoFinalTPI.Backend.Tests.Integracion;

[Trait("Categoria", "Integracion")]
public class RecuperacionContrasenaIntegracionTests(PostgisFixture postgres) : IClassFixture<PostgisFixture>
{
    [Fact]
    public async Task QueElCambioConsumaElEnlaceRevoqueLosOtrosYCierreLasSesiones()
    {
        //Arrange: base temporal del contenedor, sin User Secrets ni conexiones reales.
        var opciones = CrearOpciones();
        await using var db = new ApplicationDbContext(opciones);
        try
        {
            await db.Database.MigrateAsync();
            var usuario = CrearUsuario();
            var uno = CrearRecuperacion(usuario, new string('A', 64));
            var otro = CrearRecuperacion(usuario, new string('B', 64));
            db.AddRange(uno, otro, new SesionUsuario { Usuario = usuario, RefreshTokenHash = "sesion",
                FechaExpiracion = DateTime.UtcNow.AddDays(1) });
            await db.SaveChangesAsync();
            var repositorio = CrearRepositorio(db);
            var nueva = BCrypt.Net.BCrypt.HashPassword("clave-nueva", 4);

            //Act
            var actualizado = await repositorio.RestablecerAsync(uno.Id, uno.TokenHash, nueva);
            var reutilizado = await repositorio.RestablecerAsync(uno.Id, uno.TokenHash, "no-debe-guardarse");

            //Assert
            db.ChangeTracker.Clear();
            Assert.True(actualizado);
            Assert.False(reutilizado);
            Assert.Equal(nueva, (await db.Usuarios.SingleAsync()).Contraseña);
            Assert.NotNull((await db.RecuperacionesContrasena.SingleAsync(r => r.Id == uno.Id)).FechaUso);
            Assert.NotNull((await db.RecuperacionesContrasena.SingleAsync(r => r.Id == otro.Id)).FechaRevocacion);
            Assert.NotNull((await db.SesionesUsuario.SingleAsync()).FechaRevocacion);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task QueDosEnlacesConcurrentesSoloPermitanUnCambio()
    {
        //Arrange
        var opciones = CrearOpciones();
        await using var db = new ApplicationDbContext(opciones);
        try
        {
            await db.Database.MigrateAsync();
            var usuario = CrearUsuario();
            var uno = CrearRecuperacion(usuario, new string('A', 64));
            var otro = CrearRecuperacion(usuario, new string('B', 64));
            db.AddRange(uno, otro);
            await db.SaveChangesAsync();
            await using var contextoUno = new ApplicationDbContext(opciones);
            await using var contextoDos = new ApplicationDbContext(opciones);

            //Act
            var resultados = await Task.WhenAll(
                CrearRepositorio(contextoUno).RestablecerAsync(uno.Id, uno.TokenHash, "hash-uno"),
                CrearRepositorio(contextoDos).RestablecerAsync(otro.Id, otro.TokenHash, "hash-dos"));

            //Assert
            Assert.Single(resultados, r => r);
            db.ChangeTracker.Clear();
            var clave = (await db.Usuarios.SingleAsync()).Contraseña;
            Assert.Equal(resultados[0] ? "hash-uno" : "hash-dos", clave);
            Assert.Equal(1, await db.RecuperacionesContrasena.CountAsync(r => r.FechaUso != null));
            Assert.Equal(1, await db.RecuperacionesContrasena.CountAsync(r => r.FechaRevocacion != null));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task QueUnFalloAlRevocarSesionesDeshagaTodoElCambio()
    {
        //Arrange
        var opciones = CrearOpciones();
        await using var db = new ApplicationDbContext(opciones);
        try
        {
            await db.Database.MigrateAsync();
            var usuario = CrearUsuario();
            var recuperacion = CrearRecuperacion(usuario, new string('A', 64));
            db.Add(recuperacion);
            await db.SaveChangesAsync();
            var sesiones = new Mock<ISesionUsuarioRepositorio>();
            sesiones.Setup(s => s.RevocarTodasAsync(usuario.Id, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("fallo simulado"));
            var repositorio = new RecuperacionContrasenaRepositorio(db, sesiones.Object, TimeProvider.System);

            //Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                repositorio.RestablecerAsync(recuperacion.Id, recuperacion.TokenHash, "hash-nuevo"));
            db.ChangeTracker.Clear();
            Assert.Equal("hash-anterior", (await db.Usuarios.SingleAsync()).Contraseña);
            var guardada = await db.RecuperacionesContrasena.SingleAsync();
            Assert.Null(guardada.FechaUso);
            Assert.Null(guardada.FechaRevocacion);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task QueUnEnlaceVencidoOConHashIncorrectoNoCambieLaCuenta(bool vencido)
    {
        //Arrange
        var opciones = CrearOpciones();
        await using var db = new ApplicationDbContext(opciones);
        try
        {
            await db.Database.MigrateAsync();
            var usuario = CrearUsuario();
            var recuperacion = CrearRecuperacion(usuario, new string('A', 64));
            if (vencido) recuperacion.FechaExpiracion = DateTime.UtcNow.AddSeconds(-1);
            db.Add(recuperacion);
            await db.SaveChangesAsync();

            //Act
            var resultado = await CrearRepositorio(db).RestablecerAsync(recuperacion.Id,
                vencido ? recuperacion.TokenHash : new string('B', 64), "hash-nuevo");

            //Assert
            Assert.False(resultado);
            db.ChangeTracker.Clear();
            Assert.Equal("hash-anterior", (await db.Usuarios.SingleAsync()).Contraseña);
            Assert.Null((await db.RecuperacionesContrasena.SingleAsync()).FechaUso);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private DbContextOptions<ApplicationDbContext> CrearOpciones()
    {
        var conexion = new NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Database = "recuperacion_test_" + Guid.NewGuid().ToString("N"), Pooling = false
        }.ConnectionString;
        return new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(conexion, o => o.UseNetTopologySuite()).Options;
    }

    private static RecuperacionContrasenaRepositorio CrearRepositorio(ApplicationDbContext db) =>
        new(db, new SesionUsuarioRepositorio(db), TimeProvider.System);
    private static Usuario_Personal CrearUsuario() => new()
    {
        Username = "ana", Email = "ana@example.com", Contraseña = "hash-anterior",
        Nombre = "Ana", Apellido = "Prueba", FechaNacimiento = new DateOnly(2000, 1, 1)
    };
    private static RecuperacionContrasena CrearRecuperacion(Usuario usuario, string hash) => new()
    {
        Usuario = usuario, EmailDestino = usuario.Email, TokenHash = hash,
        FechaCreacion = DateTime.UtcNow, FechaExpiracion = DateTime.UtcNow.AddMinutes(30)
    };
}
