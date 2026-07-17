using Veolia.Api.Infrastructure.Data;
using Veolia.Api.Infrastructure.Auth;
using Veolia.Api.Infrastructure.Aps;
using Veolia.Api.Infrastructure.Sui853;
using Veolia.Api.Infrastructure.Data.Interfaces;
using Veolia.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200", "http://localhost:4201")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddScoped<IOracleConnectionFactory, OracleConnectionFactory>();
builder.Services.AddScoped<IHealthRepository, HealthRepository>();
builder.Services.AddScoped<IAuthRepository, AuthRepository>();
builder.Services.AddScoped<AuthContractMapper>();
builder.Services.AddScoped<IApsRepository, ApsRepository>();
builder.Services.AddScoped<ApsContractMapper>();
builder.Services.AddScoped<IEmpresasRepository, EmpresasRepository>();
builder.Services.AddScoped<ITarifasRepository, TarifasRepository>();
builder.Services.AddScoped<IReversionesRepository, ReversionesRepository>();
builder.Services.AddScoped<ISuministrosRepository, SuministrosRepository>();
builder.Services.AddScoped<ICertificacionRepository, CertificacionRepository>();
builder.Services.AddScoped<ISuiReversionesRepository, SuiReversionesRepository>();
builder.Services.AddScoped<ISuiRepository, SuiRepository>();
builder.Services.AddScoped<ISui853ReadmodelsRepository, Sui853ReadmodelsRepository>();
builder.Services.AddScoped<Sui853ContractMapper>();
builder.Services.AddScoped<IValidacionesRepository, ValidacionesRepository>();
builder.Services.AddScoped<ICostosRepository, CostosRepository>();
builder.Services.AddScoped<IToneladasRepository, ToneladasRepository>();
builder.Services.AddScoped<IFacturacionRepository, FacturacionRepository>();
builder.Services.AddScoped<IRellenosRepository, RellenosRepository>();
builder.Services.AddScoped<IKillometrosRepository, KillometrosRepository>();
builder.Services.AddScoped<ISubContRepository, SubContRepository>();
builder.Services.AddScoped<IProyeccionRepository, ProyeccionRepository>();
builder.Services.AddScoped<ILineaTiempoRepository, LineaTiempoRepository>();
builder.Services.AddScoped<ICrecimientoRepository, CrecimientoRepository>();
builder.Services.AddScoped<ISubcontProyRepository, SubcontProyRepository>();
builder.Services.AddScoped<IEjecucionProyeccionRepository, EjecucionProyeccionRepository>();
builder.Services.AddScoped<IReliqCrearRepository, ReliqCrearRepository>();
builder.Services.AddScoped<IReliqCargueRepository, ReliqCargueRepository>();
builder.Services.AddScoped<IReliqTarificadorRepository, ReliqTarificadorRepository>();
builder.Services.AddScoped<IInfoGeneralesRepository, InfoGeneralesRepository>();
builder.Services.AddScoped<IInfoGerencialRepository, InfoGerencialRepository>();
builder.Services.AddScoped<IPgirsRepository, PgirsRepository>();
builder.Services.AddScoped<IIndicesRepository, IndicesRepository>();
builder.Services.AddScoped<FileParserService>();

var app = builder.Build();

app.UseCors("FrontendPolicy");

var authAnonymousRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "/api/v1/auth/login",
        "/api/v1/auth/registro",
    "/api/v1/auth/getSistemasByCorreo",
    "/api/v1/auth/getSistemasPorUsuario",
    "/api/v1/auth/asignarSistema",
    "/api/v1/auth/getMenuByUser",
    "/api/v1/auth/allSistemas"
};

var apsAnonymousRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "/api/v1/aps/usuarioPorAPS"
};

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/auth", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requestPath = context.Request.Path.Value ?? string.Empty;
        return !authAnonymousRoutes.Contains(requestPath);
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/indices", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/rellenos", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/aps", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var requestPath = context.Request.Path.Value ?? string.Empty;
        return !apsAnonymousRoutes.Contains(requestPath);
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/empresas", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/tarifas", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/reversiones", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/suministros", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/sui", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/validaciones", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/facturacion", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/costos", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/toneladas", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/kilometros", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/proyecciones", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/subcon", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/reliq", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/infogenerales", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.UseWhen(
    context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/infogerencial", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    },
    branch => { branch.UseMiddleware<AuthJwtParityMiddleware>(); });

app.MapControllers();

app.Run();
