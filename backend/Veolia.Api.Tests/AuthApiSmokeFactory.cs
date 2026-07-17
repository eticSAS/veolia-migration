using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Veolia.Api.Contracts.Proyecciones;
using Veolia.Api.Infrastructure.Aps;
using Veolia.Api.Infrastructure.Data;
using Veolia.Api.Infrastructure.Sui853;

namespace Veolia.Api.Tests;

public sealed class AuthApiSmokeFactory : WebApplicationFactory<Program>
{
    internal StubSui853ReadmodelsRepository Sui853Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var sqliteFactory = new SqliteOracleConnectionFactory();

            services.RemoveAll<IAuthRepository>();
            services.AddSingleton<IAuthRepository>(new StubAuthRepository(sqliteFactory));

            services.RemoveAll<IApsRepository>();
            services.AddSingleton<IApsRepository>(new StubApsRepository());
            services.RemoveAll<ApsContractMapper>();
            services.AddSingleton<ApsContractMapper>();

            services.RemoveAll<IEmpresasRepository>();
            services.AddSingleton<IEmpresasRepository>(new StubEmpresasRepository());

            services.RemoveAll<ITarifasRepository>();
            services.AddSingleton<ITarifasRepository>(new StubTarifasRepository());

            services.RemoveAll<IProyeccionRepository>();
            services.AddSingleton<IProyeccionRepository>(new StubProyeccionRepository());

            services.RemoveAll<ILineaTiempoRepository>();
            services.AddSingleton<ILineaTiempoRepository>(new StubLineaTiempoRepository());

            services.RemoveAll<ICrecimientoRepository>();
            services.AddSingleton<ICrecimientoRepository>(new StubCrecimientoRepository());

            services.RemoveAll<ISubcontProyRepository>();
            services.AddSingleton<ISubcontProyRepository>(new StubSubcontProyRepository());

            services.RemoveAll<IEjecucionProyeccionRepository>();
            services.AddSingleton<IEjecucionProyeccionRepository>(new StubEjecucionProyeccionRepository());

            services.RemoveAll<IOracleConnectionFactory>();
            services.AddSingleton<IOracleConnectionFactory>(sqliteFactory);

            services.RemoveAll<ISui853ReadmodelsRepository>();
            services.AddSingleton<ISui853ReadmodelsRepository>(Sui853Repository);
            services.RemoveAll<Sui853ContractMapper>();
            services.AddSingleton<Sui853ContractMapper>();
        });
    }
}

internal sealed class SqliteOracleConnectionFactory : IOracleConnectionFactory
{
    private readonly string connectionString;

    public SqliteOracleConnectionFactory()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"veolia-auth-smoke-{Guid.NewGuid():N}.db");
        connectionString = $"Data Source={dbPath}";

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS AUGE_DEADTOKEN (TOKEN TEXT);";
        command.ExecuteNonQuery();
    }

    public IDbConnection CreateConnection() => new SqliteConnection(connectionString);
}

internal sealed class StubAuthRepository : IAuthRepository
{
    private readonly IOracleConnectionFactory connectionFactory;

    public StubAuthRepository(IOracleConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<object>> GetSistemasByCorreoAsync(string correo, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object> { ["SIST_ID"] = 10, ["SIST_NOMBRE"] = "Operaciones" }
        ]);

    public Task<object?> LoginAsync(string correo, string pass, int idSistema, CancellationToken cancellationToken)
        => Task.FromResult<object?>(new LoginRepositoryResult(
            LoginOutcomeKind.Success,
            "OK",
            new Dictionary<string, object>
            {
                ["SISU_ID"] = 101,
                ["SISU_NOMBRE"] = "Ada",
                ["SISU_APELLIDO"] = "Lovelace",
                ["SISU_CORREO"] = correo
            },
            new Dictionary<string, object>
            {
                ["SIST_ID"] = idSistema,
                ["SIST_NOMBRE"] = "Operaciones"
            },
            "header.payload.signature"));

    public async Task<object?> LogoutAsync(long sisuId, string token, CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.CreateConnection();
        if (connection is DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        else
        {
            connection.Open();
        }

        const string sql = "INSERT INTO AUGE_DEADTOKEN (TOKEN) VALUES (:token);";
        var rowsAffected = await connection.ExecuteAsync(new CommandDefinition(sql, new { token }, cancellationToken: cancellationToken));
        return new { rowsAffected };
    }

    public Task<IReadOnlyList<long>> GetUserMenuAsync(long sisuId, int idSistema, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<long>>([11, 22]);

    public Task<(int Status, string Response, string Msg)> SetChangePassAsync(long sisuId, string oldPass, string newPass, string confirmPass, CancellationToken cancellationToken)
        => Task.FromResult((200, "OK", "Contraseña actualizada"));

    public Task<IReadOnlyList<object>> GetAllUsersAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object>
            {
                ["SISU_ID"] = 101,
                ["SISU_NOMBRE"] = "Ada",
                ["SISU_APELLIDO"] = "Lovelace",
                ["SISU_CORREO"] = "ada@veolia.com",
                ["SISU_ESTADO"] = 1
            }
        ]);

    public Task<UserMutationRepositoryResult> RegistroAsync(string nombre, string apellido, string correo, string password, int estado, CancellationToken cancellationToken)
        => Task.FromResult(correo.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            ? new UserMutationRepositoryResult(true, null, "El correo ya se encuentra registrado")
            : new UserMutationRepositoryResult(false, new { rowsAffected = 1 }));

    public Task<UserMutationRepositoryResult> UpdateUsuarioAsync(long id, string nombre, string apellido, string correo, int estado, CancellationToken cancellationToken)
        => Task.FromResult(correo.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            ? new UserMutationRepositoryResult(true, null, "El correo ya se encuentra registrado")
            : new UserMutationRepositoryResult(false, new { rowsAffected = 1 }));

    public Task<IReadOnlyList<object>> GetUserByIdAsync(long id, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object> { ["SISU_ID"] = id, ["SISU_CORREO"] = "ada@veolia.com" }
        ]);

    public Task<string> ResetPassAsync(long id, CancellationToken cancellationToken)
        => Task.FromResult("abc123");

    public Task<(IReadOnlyList<object> Asignadas, IReadOnlyList<object> SinAsignar)> GetApsAsignadasAsync(long id, CancellationToken cancellationToken)
    {
        if (id == 500)
        {
            throw new InvalidOperationException("Simulated getApsAsignadas failure");
        }

        return Task.FromResult<(IReadOnlyList<object>, IReadOnlyList<object>)>((
            [new Dictionary<string, object> { ["APSA_ID"] = 1, ["APSA_NOMAPS"] = "APS Norte" }],
            [new Dictionary<string, object> { ["APSA_ID"] = 2, ["APSA_NOMAPS"] = "APS Sur" }]
        ));
    }

    public Task<object?> SetApsxUsuarioAsync(long id, IReadOnlyList<long> outAps, IReadOnlyList<long> inAps, CancellationToken cancellationToken)
    {
        if (id == 500)
        {
            throw new InvalidOperationException("Simulated setApsxUsuario failure");
        }

        return Task.FromResult<object?>(null);
    }

    public Task<(long SisuId, IReadOnlyList<object> Asignados, IReadOnlyList<object> SinAsignar)> GetSistemasPorUsuarioAsync(string correo, CancellationToken cancellationToken)
        => Task.FromResult<(long, IReadOnlyList<object>, IReadOnlyList<object>)>((
            1,
            [new Dictionary<string, object> { ["SIST_ID"] = 10, ["SIST_NOMBRE"] = "Operaciones" }],
            [new Dictionary<string, object> { ["SIST_ID"] = 20, ["SIST_NOMBRE"] = "Finanzas" }]
        ));

    public Task<string> AsignarSistemaAsync(long sisuId, IReadOnlyList<long> asignados, IReadOnlyList<long> noAsignados, CancellationToken cancellationToken)
        => Task.FromResult("Sistemas actualizados");

    public Task<IReadOnlyList<object>> AllSistemasAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object> { ["SIST_ID"] = 10, ["SIST_NOMBRE"] = "Operaciones" }
        ]);

    public Task<IReadOnlyList<object>> GetGeneralMenuTreeAsync(long sisuId, int idSistema, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object> { ["MENU_ID"] = 11, ["MENU_NOMBRE"] = "Dashboard" }
        ]);

    public Task<IReadOnlyList<long>> GetMenuByUserAsync(int idSistema, long sisuId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<long>>([11, 22]);

    public Task<IReadOnlyList<long>> GetMenuUserOptionsAsync(long id, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<long>>([11, 22]);

    public Task<object?> UptUserMenuAsync(long id, IReadOnlyList<long> options, int sistema, CancellationToken cancellationToken)
        => Task.FromResult<object?>(new { rowsAffected = 2 });
}

internal sealed class StubApsRepository : IApsRepository
{
    private static readonly IReadOnlyList<object> ApsCatalog =
    [
        new Dictionary<string, object>
        {
            ["APSA_ID"] = 1,
            ["APSA_NOMAPS"] = "APS Norte",
            ["APSA_RESOLUCION"] = 100,
            ["APSA_PROPIO"] = 1,
            ["APSA_SOLORELL"] = 0,
            ["APSA_ESTADO"] = 1,
            ["APSA_VIAT"] = 0,
            ["APSA_IDSUI"] = 123
        },
        new Dictionary<string, object>
        {
            ["APSA_ID"] = 2,
            ["APSA_NOMAPS"] = "APS Sur",
            ["APSA_RESOLUCION"] = 200,
            ["APSA_PROPIO"] = 0,
            ["APSA_SOLORELL"] = 1,
            ["APSA_ESTADO"] = 1,
            ["APSA_VIAT"] = 1,
            ["APSA_IDSUI"] = 456
        }
    ];

    public Task<IReadOnlyList<object>> ConsultaGeneralAsync(CancellationToken cancellationToken)
        => Task.FromResult(ApsCatalog);

    public Task<IReadOnlyList<object>> ConsultaApsAsync(long apsId, CancellationToken cancellationToken)
    {
        if (apsId <= 0 || apsId == 500)
        {
            throw new InvalidOperationException("Simulated consultaaps failure");
        }

        if (apsId == 9999)
        {
            return Task.FromResult<IReadOnlyList<object>>([]);
        }

        var row = ApsCatalog
            .Cast<Dictionary<string, object>>()
            .FirstOrDefault(item => Convert.ToInt64(item["APSA_ID"]) == apsId);

        return Task.FromResult<IReadOnlyList<object>>(row is null ? [] : [row]);
    }

    public Task<object?> CrearAsync(string nombre, int? idsui, int? resolucion, int propio, int relleno, int estado, int iat, long usuario, CancellationToken cancellationToken)
    {
        if (string.Equals(nombre, "__SQL_ERROR__", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Simulated crear failure");
        }

        return Task.FromResult<object?>(new { rowsAffected = 1 });
    }

    public Task<object?> EditarAsync(long id, string nombre, int? idsui, int? resolucion, int propio, int relleno, int estado, int iat, CancellationToken cancellationToken)
    {
        if (id <= 0 || id == 5000)
        {
            throw new InvalidOperationException("Simulated editar failure");
        }

        if (id == 4040)
        {
            return Task.FromResult<object?>(new { rowsAffected = 0 });
        }

        return Task.FromResult<object?>(new { rowsAffected = 1 });
    }

    public Task<IReadOnlyList<object>> GetApsByUsuarioAsync(long sisuId, CancellationToken cancellationToken)
    {
        if (sisuId == 500)
        {
            throw new InvalidOperationException("Simulated get aps failure");
        }

        if (sisuId == 404)
        {
            return Task.FromResult<IReadOnlyList<object>>([]);
        }

        return Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object>
            {
                ["APSA_ID"] = 1,
                ["APSA_NOMAPS"] = "APS Norte"
            }
        ]);
    }

    public Task<IReadOnlyList<object>> GetUsuarioPorApsAsync(long apsId, CancellationToken cancellationToken)
    {
        if (apsId <= 0 || apsId == 500)
        {
            throw new InvalidOperationException("Simulated get usuario por aps failure");
        }

        if (apsId == 404)
        {
            return Task.FromResult<IReadOnlyList<object>>([]);
        }

        return Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object>
            {
                ["SISU_ID"] = 1,
                ["SISU_CORREO"] = "admin@veolia.com"
            }
        ]);
    }

    public Task<object?> EliminarAsync(long id, CancellationToken cancellationToken)
    {
        if (id <= 0 || id == 5000)
        {
            throw new InvalidOperationException("Simulated eliminar aps failure");
        }

        return Task.FromResult<object?>(new { rowsAffected = 1 });
    }
}

internal sealed class StubProyeccionRepository : IProyeccionRepository
{
    public Task<IReadOnlyList<ProyeccionListItem>> ConsultaAsync(long apsaId, int anno, int mes, CancellationToken cancellationToken)
    {
        if (apsaId == 9999) return Task.FromResult<IReadOnlyList<ProyeccionListItem>>([]);
        return Task.FromResult<IReadOnlyList<ProyeccionListItem>>([
            new ProyeccionListItem { ProyId = 1, ApsaId = apsaId, ProyNombre = "Proyección Test", ProyAnnoDes = anno, ProyMesDes = mes, ProyAnnoHas = anno + 1, ProyMesHas = mes }
        ]);
    }

    public Task<IReadOnlyList<ProyeccionListItem>> ConsultaGeneralAsync(int anno, int mes, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<ProyeccionListItem>>([
            new ProyeccionListItem { ProyId = 1, ApsaId = 1, ProyNombre = "Proyección General", ProyAnnoDes = anno, ProyMesDes = mes, ProyAnnoHas = anno + 1, ProyMesHas = mes }
        ]);

    public Task<ProyeccionDetail?> ConsultaProyAsync(long proyId, CancellationToken cancellationToken)
    {
        if (proyId == 4040) return Task.FromResult<ProyeccionDetail?>(null);
        return Task.FromResult<ProyeccionDetail?>(new ProyeccionDetail { ProyId = proyId, ApsaId = 1, ProyNombre = "Detalle", ProyAnnoDes = 2025, ProyMesDes = 1, ProyAnnoHas = 2026, ProyMesHas = 12 });
    }

    public Task<MutationResponse> CrearAsync(ProyeccionCreateRequest request, long usuarioId, CancellationToken cancellationToken)
    {
        if (request.ProyNombre == "__SQL_ERROR__") throw new InvalidOperationException("Simulated crear proyeccion failure");
        return Task.FromResult(new MutationResponse { Success = true, Message = "Proyección creada", Id = 1 });
    }

    public Task<MutationResponse> EditarAsync(long proyId, ProyeccionUpdateRequest request, CancellationToken cancellationToken)
    {
        if (proyId == 5000) throw new InvalidOperationException("Simulated editar proyeccion failure");
        return Task.FromResult(new MutationResponse { Success = true, Message = "Proyección actualizada", Id = proyId });
    }

    public Task<MutationResponse> EliminarAsync(long proyId, CancellationToken cancellationToken)
    {
        if (proyId == 5000) throw new InvalidOperationException("Simulated eliminar proyeccion failure");
        return Task.FromResult(new MutationResponse { Success = true, Message = "Proyección eliminada", Id = proyId });
    }

    public Task<IReadOnlyList<object>> UltimasTarifasAsync(long apsaId, CancellationToken cancellationToken)
    {
        if (apsaId == 9999) return Task.FromResult<IReadOnlyList<object>>([]);
        return Task.FromResult<IReadOnlyList<object>>([new { anno = 2025, mes = 4 }]);
    }
}

internal sealed class StubLineaTiempoRepository : ILineaTiempoRepository
{
    public Task<IReadOnlyList<LineaTiempoRow>> GetByProyeccionAsync(long proyId, CancellationToken cancellationToken)
    {
        if (proyId == 9999) return Task.FromResult<IReadOnlyList<LineaTiempoRow>>([]);
        return Task.FromResult<IReadOnlyList<LineaTiempoRow>>([
            new LineaTiempoRow { DetlId = 1, ProyId = proyId, ApsaId = 1, Anno = 2025, Mes = 1, Deltipc = 5.2m, Deltipcc = 4.8m }
        ]);
    }

    public Task<MutationResponse> UpsertAsync(LineaTiempoUpsertRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new MutationResponse { Success = true, Message = "Línea de tiempo guardada", Id = request.ProyId });
}

internal sealed class StubCrecimientoRepository : ICrecimientoRepository
{
    public Task<CrecimientoPayload> ConsultarAsync(long proyId, CancellationToken cancellationToken)
        => Task.FromResult(new CrecimientoPayload { Usuarios = [], Propia = [], Terceros = [], Descuentos = [] });

    public Task<MutationResponse> RegistrarUsuariosAsync(CrecimientoUsuariosRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new MutationResponse { Success = true, Message = "Usuarios registrados", Id = request.ProyId });

    public Task<MutationResponse> RegistrarPropiaAsync(CrecimientoPropiaRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new MutationResponse { Success = true, Message = "Info propia registrada", Id = request.ProyId });

    public Task<MutationResponse> RegistrarTercerosAsync(CrecimientoTercerosRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new MutationResponse { Success = true, Message = "Info terceros registrada", Id = request.ProyId });

    public Task<MutationResponse> RegistrarDescuentosAsync(DescuentosRequest request, CancellationToken cancellationToken)
        => Task.FromResult(new MutationResponse { Success = true, Message = "Descuentos registrados", Id = request.ProyId });
}

internal sealed class StubSubcontProyRepository : ISubcontProyRepository
{
    public Task<IReadOnlyList<SubcontItem>> GetSubcontAsync(SubcontConsultaRequest request, CancellationToken cancellationToken)
    {
        if (request.ApsaId == 9999) return Task.FromResult<IReadOnlyList<SubcontItem>>([]);
        return Task.FromResult<IReadOnlyList<SubcontItem>>([
            new SubcontItem { ClasClase = 1, SucoValor = 15.5m }
        ]);
    }

    public Task<MutationResponse> UpsertSubcontAsync(SubcontUpsertRequest request, long usuarioId, CancellationToken cancellationToken)
        => Task.FromResult(new MutationResponse { Success = true, Message = "Subcont actualizado", Id = request.ProyId });
}

internal sealed class StubEjecucionProyeccionRepository : IEjecucionProyeccionRepository
{
    public Task<string> EjecutarProyectarAsync(long proyId, long apsaId, long usuarioId, CancellationToken cancellationToken)
    {
        if (proyId == 5000) throw new InvalidOperationException("Simulated ejecutar proyeccion failure");
        return Task.FromResult("STUB_OK");
    }
}

internal sealed class StubSui853ReadmodelsRepository : ISui853ReadmodelsRepository
{
    private int failEmpresa;
    private int failDocumento;
    private int failTcfg;

    public void FailNextEmpresa() => Interlocked.Exchange(ref failEmpresa, 1);
    public void FailNextDocumento() => Interlocked.Exchange(ref failDocumento, 1);
    public void FailNextTcfg() => Interlocked.Exchange(ref failTcfg, 1);

    public Task<IReadOnlyList<object>> GetVcfgApsEmpresaAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref failEmpresa, 0) == 1)
        {
            throw new InvalidOperationException("Simulated vcfgapsempresa failure");
        }

        return Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object>
            {
                ["TCFG_APS_ID"] = 10,
                ["NOMAPS"] = "APS Norte",
                ["NUAP"] = "1001",
                ["EMPRESA"] = "Veolia",
                ["CODSUI"] = "COD-01",
                ["DEPARTAMENTO"] = "Antioquia",
                ["MUNICIPIO"] = "Medellin"
            }
        ]);
    }

    public Task<IReadOnlyList<object>> GetVcfgApsDocumentoAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref failDocumento, 0) == 1)
        {
            throw new InvalidOperationException("Simulated vcfgapsdocumento failure");
        }

        return Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object>
            {
                ["NOMAPS"] = "APS Norte",
                ["SEGMENTO"] = "Residencial",
                ["CODFORMATO"] = "F01",
                ["NOMFORMATO"] = "Formato 1"
            }
        ]);
    }

    public Task<IReadOnlyList<object>> GetTcfgApsAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref failTcfg, 0) == 1)
        {
            throw new InvalidOperationException("Simulated tcfgAps failure");
        }

        return Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object>
            {
                ["TCFG_APS_ID"] = 1,
                ["NOMBRE_APS"] = "APS Centro"
            },
            new Dictionary<string, object>
            {
                ["TCFG_APS_ID"] = 2,
                ["NOMBRE_APS"] = "APS Norte"
            }
        ]);
    }
}

internal sealed class StubEmpresasRepository : IEmpresasRepository
{
    private static readonly IReadOnlyList<object> EmpresasCatalog =
    [
        new Dictionary<string, object>
        {
            ["EMPR_EMPR"] = 1,
            ["EMPR_NOMBRE"] = "Aseo Norte",
            ["EMPR_ESTADO"] = 1,
            ["EMPR_PROPIA"] = 1,
            ["EMPR_NUAP"] = "NUAP-001"
        },
        new Dictionary<string, object>
        {
            ["EMPR_EMPR"] = 2,
            ["EMPR_NOMBRE"] = "Beta Limpieza",
            ["EMPR_ESTADO"] = 1,
            ["EMPR_PROPIA"] = 0,
            ["EMPR_NUAP"] = "NUAP-002"
        }
    ];

    public Task<IReadOnlyList<object>> GetAllAsync(CancellationToken cancellationToken)
        => Task.FromResult(EmpresasCatalog);

    public Task<object?> CreateAsync(string nombre, int estado, int propia, string? nuap, long sisuId, CancellationToken cancellationToken)
    {
        if (string.Equals(nombre, "__SQL_ERROR__", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Simulated crear empresa failure");
        }

        if (sisuId == 0)
        {
            throw new InvalidOperationException("Missing SISU_ID token context");
        }

        return Task.FromResult<object?>(new { rowsAffected = 1 });
    }

    public Task<IReadOnlyList<object>> ConsultarPropiasAsync(long aps, int propia, CancellationToken cancellationToken)
    {
        if (aps <= 0 || aps == 500)
        {
            throw new InvalidOperationException("Simulated consultarpropias failure");
        }

        if (aps == 9999)
        {
            return Task.FromResult<IReadOnlyList<object>>([]);
        }

        var filtered = EmpresasCatalog
            .Cast<Dictionary<string, object>>()
            .Where(item => Convert.ToInt32(item["EMPR_PROPIA"]) == propia)
            .Cast<object>()
            .ToList();

        return Task.FromResult<IReadOnlyList<object>>(filtered);
    }

    public Task<IReadOnlyList<object>> ConsultaEmprAsync(long empr, CancellationToken cancellationToken)
    {
        if (empr <= 0 || empr == 500)
        {
            throw new InvalidOperationException("Simulated consultaempr failure");
        }

        if (empr == 4040)
        {
            return Task.FromResult<IReadOnlyList<object>>([]);
        }

        var row = EmpresasCatalog
            .Cast<Dictionary<string, object>>()
            .FirstOrDefault(item => Convert.ToInt64(item["EMPR_EMPR"]) == empr);

        return Task.FromResult<IReadOnlyList<object>>(row is null ? [] : [row]);
    }

    public Task<object?> UpdateAsync(long id, string nombre, int estado, int propia, string? nuap, CancellationToken cancellationToken)
    {
        if (id <= 0 || id == 5000)
        {
            throw new InvalidOperationException("Simulated editar empresa failure");
        }

        if (id == 4040)
        {
            return Task.FromResult<object?>(new { rowsAffected = 0 });
        }

        return Task.FromResult<object?>(new { rowsAffected = 1 });
    }

    public Task<object?> EliminarAsync(long id, CancellationToken cancellationToken)
    {
        if (id <= 0 || id == 5000)
        {
            throw new InvalidOperationException("Simulated eliminar empresa failure");
        }

        return Task.FromResult<object?>(new { rowsAffected = 1 });
    }

    public Task<object?> ToggleEstadoAsync(long id, CancellationToken cancellationToken)
    {
        if (id <= 0 || id == 5000)
        {
            throw new InvalidOperationException("Simulated toggle estado empresa failure");
        }

        return Task.FromResult<object?>(new { rowsAffected = 1 });
    }
}

internal sealed class StubTarifasRepository : ITarifasRepository
{
    public Task<IReadOnlyList<object>> ConsultaTarifaAsync(long aps, int anno, int mes, CancellationToken cancellationToken)
    {
        if (aps <= 0 || anno <= 0 || mes <= 0)
        {
            throw new InvalidOperationException("Simulated consulta tarifa failure");
        }

        if (aps == 9999)
        {
            return Task.FromResult<IReadOnlyList<object>>([]);
        }

        return Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object>
            {
                ["APSA_ID"] = aps,
                ["TARI_ANNO"] = anno,
                ["TARI_MES"] = mes,
                ["TARI_VALOR"] = 123.456789m
            }
        ]);
    }

    public Task<IReadOnlyList<object>> ConsultaGeneralAsync(int anno, int mes, CancellationToken cancellationToken)
    {
        if (anno <= 0 || mes <= 0)
        {
            throw new InvalidOperationException("Simulated consulta general failure");
        }

        if (anno == 2099)
        {
            return Task.FromResult<IReadOnlyList<object>>([]);
        }

        return Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object>
            {
                ["APSA_ID"] = 1,
                ["TARI_ANNO"] = anno,
                ["TARI_MES"] = mes,
                ["TARI_VALOR"] = 111.111111m
            },
            new Dictionary<string, object>
            {
                ["APSA_ID"] = 2,
                ["TARI_ANNO"] = anno,
                ["TARI_MES"] = mes,
                ["TARI_VALOR"] = 222.222222m
            }
        ]);
    }

    public Task<IReadOnlyList<object>> TarifaPorComponenteAsync(long aps, int anno, int mes, CancellationToken cancellationToken)
    {
        if (aps <= 0 || anno <= 0 || mes <= 0)
        {
            throw new InvalidOperationException("Simulated tarifa por componente failure");
        }

        if (aps == 7777)
        {
            return Task.FromResult<IReadOnlyList<object>>([]);
        }

        return Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object>
            {
                ["APSA_ID"] = aps,
                ["TARI_ANNO"] = anno,
                ["TARI_MES"] = mes,
                ["TACO_CONCEPTO"] = "Barrido",
                ["TACO_VALOR"] = 10.987654m
            }
        ]);
    }

    public Task<IReadOnlyList<object>> TarifaPorComponenteGeneralAsync(int anno, int mes, CancellationToken cancellationToken)
    {
        if (anno <= 0 || mes <= 0)
        {
            throw new InvalidOperationException("Simulated tarifa por componente general failure");
        }

        if (anno == 2098)
        {
            return Task.FromResult<IReadOnlyList<object>>([]);
        }

        return Task.FromResult<IReadOnlyList<object>>([
            new Dictionary<string, object>
            {
                ["apsa_nomaps"] = "APS Norte",
                ["APSA_ID"] = 1,
                ["TARI_ANNO"] = anno,
                ["TARI_MES"] = mes,
                ["TACO_CONCEPTO"] = "Recolección",
                ["TACO_VALOR"] = 20.123456m
            }
        ]);
    }
}
