using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Neo4j.Driver;
using NetTopologySuite;
using Npgsql;
using ProyectoFinalTPI.Backend.Entidades;
using ProyectoFinalTPI.Backend.Interfaces.Repositorio;
using ProyectoFinalTPI.Backend.Interfaces.Servicio;
using ProyectoFinalTPI.Backend.Repositorio;
using ProyectoFinalTPI.Backend.Repositorio.Data;
using ProyectoFinalTPI.Backend.Servicio;
using ProyectoFinalTPI.Backend.Services;
using ProyectoFinalTPI.Backend.Hubs;
using ProyectoFinalTPI.Backend.Swagger;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth-intentos", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        var origenes = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:3001", "https://localhost:3001"];
        policy.WithOrigins(origenes)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // Las coordenadas multipart usan punto decimal, independientemente del servidor.
    var formFactory = options.ValueProviderFactories.OfType<Microsoft.AspNetCore.Mvc.ModelBinding.FormValueProviderFactory>().Single();
    options.ValueProviderFactories.Remove(formFactory);
    options.ValueProviderFactories.Insert(0, new ProyectoFinalTPI.Backend.Requests.InvariantFormValueProviderFactory());
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgresConnection"),
        npgsqlOptions => npgsqlOptions.UseNetTopologySuite()
    ));

builder.Services.AddSingleton<IDriver>(provider =>
{
    var uri = builder.Configuration["Neo4j:Uri"];
    var user = builder.Configuration["Neo4j:User"];
    var password = builder.Configuration["Neo4j:Password"];

    return GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
});

builder.Services.AddScoped<IPublicacionRepositorio, PublicacionRepositorio>();
builder.Services.AddScoped<IPublicacionServicio, PublicacionServicio>();
builder.Services.AddScoped<IComentarioRepositorio, ComentarioRepositorio>();
builder.Services.AddScoped<IComentarioServicio, ComentarioServicio>();
builder.Services.AddScoped<IEtiquetadoPublicacionServicio, EtiquetadoPublicacionServicio>();
builder.Services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
builder.Services.AddScoped<ISesionUsuarioRepositorio, SesionUsuarioRepositorio>();
builder.Services.AddScoped<ISeguimientoServicio, SeguimientoServicio>();
builder.Services.AddScoped<IMultimediaServicio, MultimediaServicio>();
builder.Services.AddScoped<IInteresServicio, InteresServicio>();
builder.Services.AddScoped<IRecomendacionServicio, RecomendacionServicio>();
builder.Services.AddScoped<IUsuarioServicio, UsuarioServicio>();

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<
    ISesionFypRepositorio,
    SesionFypRepositorio>();

// Auth
builder.Services.AddScoped<IAuthServicio, AuthServicio>();

// ── Chat ───────────────────────────────────────────────────────────────
builder.Services.AddSignalR();

// Singleton: el diccionario de presencia debe vivir toda la app.
builder.Services.AddSingleton<PresenciaService>();

// Scoped: ChatServicio usa IHubContext y el repositorio, ambos compatibles con Scoped.
builder.Services.AddScoped<IChatServicio, ChatServicio>();
builder.Services.AddScoped<IMensajeChatRepositorio, MensajeChatRepositorio>();

// Hosted service: corre en background y limpia mensajes expirados cada hora.
builder.Services.AddHostedService<LimpiezaMensajesService>();
// ─────────────────────────────────────────────────────────────────────────────

var claveJwt = builder.Configuration["Jwt:Clave"]
    ?? (builder.Environment.EnvironmentName == "Testing"
        ? "clave-de-testing-no-usar-en-produccion-32ch"
        : throw new InvalidOperationException("Jwt:Clave no está configurada. Ejecutá: dotnet user-secrets set \"Jwt:Clave\" \"<clave-de-al-menos-32-caracteres>\""));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Emisor"] ?? "nostalgia-backend",
            ValidAudience = builder.Configuration["Jwt:Audiencia"] ?? "nostalgia-frontend",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(claveJwt)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // Necesario para que SignalR (chat) pueda recibir el token por query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                var sid = context.Principal?.FindFirst("sid")?.Value;
                if (!Guid.TryParseExact(sid, "N", out var sesionId))
                {
                    context.Fail("El token no contiene una sesión válida.");
                    return;
                }

                var sesiones = context.HttpContext.RequestServices.GetRequiredService<ISesionUsuarioRepositorio>();
                if (!await sesiones.EstaActivaAsync(sesionId, context.HttpContext.RequestAborted))
                    context.Fail("La sesión fue revocada o expiró.");
            }
        };
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Nostalgia API", Version = "v1" });

    // Inyecta X-Nostalgiar-Request: 1 automáticamente en todos los requests desde Swagger
    options.OperationFilter<NostalgiarRequestHeaderFilter>();

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Pegá el token JWT acá. Lo obtenés del endpoint /api/auth/login."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Nostalgia API v1");
    options.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
if (app.Environment.IsDevelopment())
{
    ProyectoFinalTPI.Backend.DataSeeder.Seed(app.Services, app.Environment);
}
if (args.Contains("--seed-only"))
{
    if (!app.Environment.IsDevelopment())
        throw new InvalidOperationException("--seed-only requiere el entorno Development.");
    return;
}
app.UseMiddleware<ProyectoFinalTPI.Backend.Middlewares.ManejoExcepcionesMiddleware>();

app.UseRouting();

app.UseCors("AllowFrontend");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();

// ── Ruta del Hub de chat ───────────────────────────────────────────────
app.MapHub<ChatHub>("/hubs/chat");
// ─────────────────────────────────────────────────────────────────────────────

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program { }
