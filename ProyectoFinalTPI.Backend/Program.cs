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

if (args.Contains("--probar-gemini"))
{
    await PruebaGemini.Ejecutar(builder.Configuration);
    return;
}

if (args.Contains("--previsualizar-reprocesamiento"))
{
    await PrevisualizarReprocesamiento.Ejecutar(builder.Configuration, args);
    return;
}

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
builder.Services.AddScoped<IRelacionRecuerdosRepositorio, RelacionRecuerdosRepositorio>();
builder.Services.AddScoped<IRelacionRecuerdosServicio, RelacionRecuerdosServicio>();
builder.Services.AddScoped<IMomentoRepositorio, MomentoRepositorio>();
builder.Services.AddScoped<IMomentoServicio, MomentoServicio>();
builder.Services.AddScoped<IGestionDuplicadosMomentoRepositorio, MomentoRepositorio>();
builder.Services.AddScoped<IGestionDuplicadosMomentoServicio, GestionDuplicadosMomentoServicio>();
builder.Services.AddScoped<IExploracionMomentosRepositorio, MomentoRepositorio>();
builder.Services.AddScoped<IExploracionMomentosServicio, ExploracionMomentosServicio>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<OpcionesAnalisisRecuerdos>()
    .Bind(builder.Configuration.GetSection("AnalisisRecuerdos"))
    .Configure(o =>
    {
        if (builder.Environment.EnvironmentName == "Testing" && builder.Configuration["AnalisisRecuerdos:Habilitado"] == null)
            o.Habilitado = false;
    })
    .ValidateDataAnnotations()
    .Validate(o => o.ReservaSegundos > o.TiempoMaximoSegundos + 5, "La reserva debe superar el tiempo máximo de análisis en más de 5 segundos.")
    .ValidateOnStart();
builder.Services.AddScoped<ITrabajoAnalisisRepositorio, TrabajoAnalisisRepositorio>();
builder.Services.AddScoped<IReprocesamientoRecuerdosRepositorio, ReprocesamientoRecuerdosRepositorio>();
builder.Services.AddScoped<IReprocesamientoRecuerdosServicio, ReprocesamientoRecuerdosServicio>();
builder.Services.AddScoped<ICatalogoAnalisisRepositorio, CatalogoAnalisisRepositorio>();
builder.Services.AddOptions<OpcionesGemini>()
    .Bind(builder.Configuration.GetSection("Gemini"))
    .Configure(o => { if (builder.Environment.EnvironmentName == "Testing") o.Habilitado = false; })
    .ValidateDataAnnotations()
    .Validate(o => !o.Habilitado || !string.IsNullOrWhiteSpace(o.ApiKey), "Gemini:ApiKey es obligatoria al habilitar Gemini.")
    .ValidateOnStart();
builder.Services.AddSingleton<ControlCuotaGemini>();
builder.Services.AddHttpClient<AnalizadorRecuerdoGemini>(http => http.Timeout = Timeout.InfiniteTimeSpan)
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<IAnalizadorRecuerdo>(sp =>
    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpcionesGemini>>().Value.Habilitado
        ? sp.GetRequiredService<AnalizadorRecuerdoGemini>() : new AnalizadorRecuerdoSinProveedor());
builder.Services.AddScoped<IAnalisisRecuerdosServicio, AnalisisRecuerdosServicio>();
builder.Services.AddHostedService<AnalisisRecuerdosWorker>();
builder.Services.AddScoped<IPublicacionServicio, PublicacionServicio>();
builder.Services.AddOptions<FeedRecomendacionesOptions>()
    .Bind(builder.Configuration.GetSection(FeedRecomendacionesOptions.Seccion))
    .Validate(opciones => opciones.DiasDeExclusionDeImpresiones > 0
        && opciones.CantidadMaximaCandidatas > 0
        && opciones.PesoLike >= 0
        && opciones.PesoPuntuacion >= 0
        && opciones.PesoEtiqueta >= 0
        && opciones.PesoAfinidadCategoria >= 0
        && opciones.PesoCercania >= 0
        && opciones.DistanciaDeReferenciaKm > 0,
        "Los parámetros de FeedRecomendaciones deben ser positivos; los pesos pueden ser cero.")
    .ValidateOnStart();
builder.Services.AddScoped<IComentarioRepositorio, ComentarioRepositorio>();
builder.Services.AddScoped<IComentarioServicio, ComentarioServicio>();
builder.Services.AddScoped<IGrupoRepositorio, GrupoRepositorio>();
builder.Services.AddScoped<IGrupoServicio, GrupoServicio>();
builder.Services.AddScoped<IEtiquetadoPublicacionServicio, EtiquetadoPublicacionServicio>();
builder.Services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
builder.Services.AddScoped<ISesionUsuarioRepositorio, SesionUsuarioRepositorio>();
builder.Services.AddScoped<ISeguimientoServicio, SeguimientoServicio>();
builder.Services.AddScoped<IMultimediaServicio, MultimediaServicio>();
builder.Services.AddScoped<IInteresServicio, InteresServicio>();
builder.Services.AddScoped<IUsuarioServicio, UsuarioServicio>();

builder.Services.AddMemoryCache();

builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddHttpClient("GooglePhotos", client => client.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
    .RemoveAllLoggers();
builder.Services.AddSingleton<GooglePhotosService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<GooglePhotosService>());

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

// ── Notificaciones ────────────────────────────────────────────────────
builder.Services.AddScoped<INotificacionRepositorio, NotificacionRepositorio>();
builder.Services.AddScoped<INotificacionPush, NotificacionPush>();        // ← AGREGAR
builder.Services.AddScoped<INotificacionServicio, NotificacionServicio>();
builder.Services.AddHostedService<LimpiezaNotificacionesService>();
// ─────────────────────────────────────────────────────────────────────

var claveJwt = builder.Configuration["Jwt:Clave"]
    ?? (builder.Environment.EnvironmentName == "Testing"
        ? "clave-de-testing-no-usar-en-produccion-32ch"
        : throw new InvalidOperationException("Jwt:Clave no está configurada. Ejecutá: dotnet user-secrets set \"Jwt:Clave\" \"<clave-de-al-menos-32-caracteres>\""));

builder.Services.AddAuthorization(options => options.AddPolicy("AdministrarAnalisis", policy =>
    policy.RequireAuthenticatedUser().RequireClaim("esAdmin", "true")));

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
    options.EnableAnnotations();
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
    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

    options.IncludeXmlComments(xmlPath);
    var filtrosXml = $"{typeof(ProyectoFinalTPI.Backend.Dtos.Publicacion.PublicacionFiltroDto).Assembly.GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, filtrosXml));
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

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
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
// ── Ruta del Hub de notificaciones ───────────────────────────────────────────────
app.MapHub<NotificacionHub>("/hubs/notificaciones");
// ─────────────────────────────────────────────────────────────────────────────

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

public partial class Program { }
