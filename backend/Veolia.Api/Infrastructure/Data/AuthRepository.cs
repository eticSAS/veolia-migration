using Dapper;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Veolia.Api.Infrastructure.Data;

public enum LoginOutcomeKind
{
    Success,
    InvalidCredentials,
    InvalidSystem
}

public sealed record LoginRepositoryResult(
    LoginOutcomeKind Kind,
    string Message,
    object? Usuario = null,
    object? Sistema = null,
    string? AuthToken = null);

public sealed record UserMutationRepositoryResult(
    bool IsDuplicateEmail,
    object? Payload,
    string? Message = null);

public class AuthRepository(IOracleConnectionFactory connectionFactory, IConfiguration configuration) : IAuthRepository
{
    public async Task<IReadOnlyList<object>> GetSistemasByCorreoAsync(string correo, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    s.SIST_ID,
    s.SIST_NOMBRE
FROM AUGE_SISUSUARIO su
INNER JOIN AUGE_USUASISTEMA us ON us.USUA_ID = su.SISU_ID
INNER JOIN AUGE_SISTEMA s ON s.SIST_ID = us.SIST_ID
WHERE LOWER(su.SISU_CORREO) = LOWER(:correo)
  AND su.SISU_ESTADO = 1
  AND us.USSI_ESTADO = 1
  AND s.SIST_ESTADO = 1
ORDER BY s.SIST_NOMBRE";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(sql, new { correo });
        return rows.Select(ToDictionaryObject).ToList();
    }

    public async Task<object?> LoginAsync(string correo, string pass, int idSistema, CancellationToken cancellationToken)
    {
        const string userSql = @"
SELECT
    SISU_ID,
    SISU_NOMBRES AS SISU_NOMBRE,
    SISU_APELLIDOS AS SISU_APELLIDO,
    SISU_CORREO,
    SISU_PASS,
    SISU_ESTADO
FROM AUGE_SISUSUARIO
WHERE LOWER(SISU_CORREO) = LOWER(:correo)
  AND SISU_ESTADO = 1";

        const string sistemaSql = @"
SELECT
    s.SIST_ID,
    s.SIST_NOMBRE
FROM AUGE_USUASISTEMA us
INNER JOIN AUGE_SISTEMA s ON s.SIST_ID = us.SIST_ID
WHERE us.USUA_ID = :sisuId
  AND us.SIST_ID = :idSistema
  AND us.USSI_ESTADO = 1
  AND s.SIST_ESTADO = 1";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var userRow = await connection.QueryFirstOrDefaultAsync(userSql, new { correo });
        if (userRow is null)
        {
            return new LoginRepositoryResult(LoginOutcomeKind.InvalidCredentials, "Correo o contraseña inválida");
        }

        var user = ToDictionary(userRow);
        user.TryGetValue("SISU_PASS", out object? storedHashObj);
        var storedHash = storedHashObj as string;
        if (string.IsNullOrWhiteSpace(storedHash) || !BCrypt.Net.BCrypt.Verify(pass, storedHash))
        {
            return new LoginRepositoryResult(LoginOutcomeKind.InvalidCredentials, "Correo o contraseña inválida");
        }

        user.Remove("SISU_PASS");
        var sisuId = ReadLong(user, "SISU_ID");
        if (sisuId <= 0)
        {
            return new LoginRepositoryResult(LoginOutcomeKind.InvalidCredentials, "Correo o contraseña inválida");
        }

        var sistemaRow = await connection.QueryFirstOrDefaultAsync(sistemaSql, new { sisuId, idSistema });
        if (sistemaRow is null)
        {
            return new LoginRepositoryResult(LoginOutcomeKind.InvalidSystem, "Sistema no encontrado para el usuario");
        }

        var sistema = ToDictionaryObject(sistemaRow);
        var authToken = BuildParityJwtToken(sisuId, idSistema);

        return new LoginRepositoryResult(
            LoginOutcomeKind.Success,
            "OK",
            Usuario: user,
            Sistema: sistema,
            AuthToken: authToken);
    }

    public async Task<object?> LogoutAsync(long sisuId, string token, CancellationToken cancellationToken)
    {
        using var connection = await OpenConnectionAsync(cancellationToken);

        // AS-IS parity with legacy auth/controller.js:
        // INSERT INTO AUGE_DEADTOKEN VALUES (SAUGE_DEADTOKEN.NEXTVAL, :token, :usuario, sysdate)
        const string sql = "INSERT INTO AUGE_DEADTOKEN VALUES (SAUGE_DEADTOKEN.NEXTVAL, :token, :sisuId, SYSDATE)";

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { token, sisuId }, cancellationToken: cancellationToken));

        return new { rowsAffected };
    }

    public async Task<IReadOnlyList<long>> GetUserMenuAsync(long sisuId, int idSistema, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT um.MENU_ID
FROM AUGE_USUAMENU um
INNER JOIN AUGE_MENU m ON m.MENU_ID = um.MENU_ID
WHERE um.SISU_ID = :sisuId
  AND um.USME_ESTADO = 1
  AND m.MENU_ESTADO = 1
  AND m.MENU_SISTEMA = :idSistema
ORDER BY um.MENU_ID";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<long>(sql, new { sisuId, idSistema });
        return rows.ToList();
    }

    public async Task<(int Status, string Response, string Msg)> SetChangePassAsync(long sisuId, string oldPass, string newPass, string confirmPass, CancellationToken cancellationToken)
    {
        if (!string.Equals(newPass, confirmPass, StringComparison.Ordinal))
        {
            return (403, "Las contraseñas no coinciden", "Las contraseñas no coinciden");
        }

        const string currentPassSql = @"
SELECT SISU_PASS
FROM AUGE_SISUSUARIO
WHERE SISU_ID = :sisuId
  AND SISU_ESTADO = 1";

        const string updateSql = @"
UPDATE AUGE_SISUSUARIO
SET SISU_PASS = :newPass
WHERE SISU_ID = :sisuId
  AND SISU_ESTADO = 1";

        try
        {
            using var connection = await OpenConnectionAsync(cancellationToken);
            var currentPass = await connection.QueryFirstOrDefaultAsync<string>(
                new CommandDefinition(currentPassSql, new { sisuId }, cancellationToken: cancellationToken));

            if (string.IsNullOrEmpty(currentPass))
            {
                return (403, "Usuario no encontrado", "Usuario no encontrado");
            }

            if (!BCrypt.Net.BCrypt.Verify(oldPass, currentPass))
            {
                return (403, "Contraseña actual inválida", "Contraseña actual inválida");
            }

            var hashedNewPass = BCrypt.Net.BCrypt.HashPassword(newPass, workFactor: 10);
            var rowsAffected = await connection.ExecuteAsync(
                new CommandDefinition(updateSql, new { newPass = hashedNewPass, sisuId }, cancellationToken: cancellationToken));

            if (rowsAffected <= 0)
            {
                return (500, "No se actualizó la contraseña", "No se actualizó la contraseña");
            }

            return (200, "OK", "Contraseña actualizada");
        }
        catch (Exception ex)
        {
            return (500, ex.Message, "No se pudo cambiar la contraseña");
        }
    }

    public async Task<IReadOnlyList<object>> GetAllUsersAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    SISU_ID,
    SISU_NOMBRES AS SISU_NOMBRE,
    SISU_APELLIDOS AS SISU_APELLIDO,
    SISU_CORREO,
    SISU_ESTADO
FROM AUGE_SISUSUARIO
ORDER BY SISU_APELLIDOS, SISU_NOMBRES, SISU_ID";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(sql);
        return rows.Select(ToDictionaryObject).ToList();
    }

    public async Task<UserMutationRepositoryResult> RegistroAsync(string nombre, string apellido, string correo, string password, int estado, CancellationToken cancellationToken)
    {
        if (await EmailExistsAsync(correo, null, cancellationToken))
        {
            return new UserMutationRepositoryResult(true, null, DuplicateEmailMessage);
        }

        const string sql = @"
INSERT INTO AUGE_SISUSUARIO (
    SISU_ID,
    SISU_NOMBRES,
    SISU_APELLIDOS,
    SISU_CORREO,
    SISU_PASS,
    SISU_ESTADO,
    SISU_FECHA
)
VALUES (
    SAUGE_SISUSUARIO.NEXTVAL,
    :nombre,
    :apellido,
    :correo,
    :password,
    :estado,
    SYSDATE
)";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var hashedPassword = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 10);
        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { nombre, apellido, correo, password = hashedPassword, estado }, cancellationToken: cancellationToken));

        return new UserMutationRepositoryResult(false, new { rowsAffected });
    }

    public async Task<UserMutationRepositoryResult> UpdateUsuarioAsync(long id, string nombre, string apellido, string correo, int estado, CancellationToken cancellationToken)
    {
        if (await EmailExistsAsync(correo, id, cancellationToken))
        {
            return new UserMutationRepositoryResult(true, null, DuplicateEmailMessage);
        }

        const string sql = @"
UPDATE AUGE_SISUSUARIO
SET SISU_NOMBRES = :nombre,
    SISU_APELLIDOS = :apellido,
    SISU_CORREO = :correo,
    SISU_ESTADO = :estado
WHERE SISU_ID = :id";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { id, nombre, apellido, correo, estado }, cancellationToken: cancellationToken));

        return new UserMutationRepositoryResult(false, new { rowsAffected });
    }

    public async Task<IReadOnlyList<object>> GetUserByIdAsync(long id, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    SISU_ID,
    SISU_NOMBRES AS SISU_NOMBRE,
    SISU_APELLIDOS AS SISU_APELLIDO,
    SISU_CORREO,
    SISU_ESTADO
FROM AUGE_SISUSUARIO
WHERE SISU_ID = :id";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(sql, new { id });
        return rows.Select(ToDictionaryObject).ToList();
    }

    public async Task<string> ResetPassAsync(long id, CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE AUGE_SISUSUARIO
SET SISU_PASS = :newPass
WHERE SISU_ID = :id";

        var newPass = GeneratePassword();
        var hashedNewPass = BCrypt.Net.BCrypt.HashPassword(newPass, workFactor: 10);

        using var connection = await OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(sql, new { id, newPass = hashedNewPass }, cancellationToken: cancellationToken));

        return newPass;
    }

    public async Task<(IReadOnlyList<object> Asignadas, IReadOnlyList<object> SinAsignar)> GetApsAsignadasAsync(long id, CancellationToken cancellationToken)
    {
        const string asignadasSql = @"
SELECT
    a.APSA_ID,
    a.APSA_NOMAPS
FROM AUCO_APSUSUARIOS au
INNER JOIN AUCO_APSASEO a ON a.APSA_ID = au.APSA_ID
WHERE au.SISU_ID = :id
  AND au.APSI_ESTADO = 1
  AND a.APSA_ESTADO = 1
ORDER BY a.APSA_NOMAPS";

        const string sinAsignarSql = @"
SELECT
    a.APSA_ID,
    a.APSA_NOMAPS
FROM AUCO_APSASEO a
WHERE a.APSA_ESTADO = 1
  AND NOT EXISTS (
      SELECT 1
      FROM AUCO_APSUSUARIOS au
      WHERE au.APSA_ID = a.APSA_ID
        AND au.SISU_ID = :id
        AND au.APSI_ESTADO = 1
  )
ORDER BY a.APSA_NOMAPS";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var asignadas = await connection.QueryAsync(asignadasSql, new { id });
        var sinAsignar = await connection.QueryAsync(sinAsignarSql, new { id });

        return (
            asignadas.Select(ToDictionaryObject).ToList(),
            sinAsignar.Select(ToDictionaryObject).ToList());
    }

    public async Task<object?> SetApsxUsuarioAsync(long id, IReadOnlyList<long> outAps, IReadOnlyList<long> inAps, CancellationToken cancellationToken)
    {
        const string updateInactiveSql = @"
UPDATE AUCO_APSUSUARIOS
SET APSI_ESTADO = 0
WHERE SISU_ID = :id
  AND APSA_ID = :apsaId";

        const string mergeActiveSql = @"
MERGE INTO AUCO_APSUSUARIOS t
USING (
    SELECT :id AS SISU_ID, :apsaId AS APSA_ID
    FROM DUAL
) src
ON (t.SISU_ID = src.SISU_ID AND t.APSA_ID = src.APSA_ID)
WHEN MATCHED THEN
    UPDATE SET t.APSI_ESTADO = 1, t.APSI_FECREA = SYSDATE
WHEN NOT MATCHED THEN
    INSERT (APSA_ID, SISU_ID, APSI_ESTADO, APSI_FECREA)
    VALUES (src.APSA_ID, src.SISU_ID, 1, SYSDATE)";

        using var connection = await OpenConnectionAsync(cancellationToken);

        foreach (var apsaId in outAps.Distinct())
        {
            await connection.ExecuteAsync(
                new CommandDefinition(updateInactiveSql, new { id, apsaId }, cancellationToken: cancellationToken));
        }

        foreach (var apsaId in inAps.Distinct())
        {
            await connection.ExecuteAsync(
                new CommandDefinition(mergeActiveSql, new { id, apsaId }, cancellationToken: cancellationToken));
        }

        // Legacy quirk: route may return empty body on success.
        return null;
    }

    public async Task<(long SisuId, IReadOnlyList<object> Asignados, IReadOnlyList<object> SinAsignar)> GetSistemasPorUsuarioAsync(string correo, CancellationToken cancellationToken)
    {
        const string usuarioSql = @"
SELECT SISU_ID
FROM AUGE_SISUSUARIO
WHERE LOWER(SISU_CORREO) = LOWER(:correo)
  AND SISU_ESTADO = 1";

        const string asignadosSql = @"
SELECT
    s.SIST_ID,
    s.SIST_NOMBRE
FROM AUGE_USUASISTEMA us
INNER JOIN AUGE_SISTEMA s ON s.SIST_ID = us.SIST_ID
WHERE us.USUA_ID = :sisuId
  AND us.USSI_ESTADO = 1
  AND s.SIST_ESTADO = 1
ORDER BY s.SIST_NOMBRE";

        const string sinAsignarSql = @"
SELECT
    s.SIST_ID,
    s.SIST_NOMBRE
FROM AUGE_SISTEMA s
WHERE s.SIST_ESTADO = 1
  AND NOT EXISTS (
      SELECT 1
      FROM AUGE_USUASISTEMA us
      WHERE us.SIST_ID = s.SIST_ID
        AND us.USUA_ID = :sisuId
        AND us.USSI_ESTADO = 1
  )
ORDER BY s.SIST_NOMBRE";

        using var connection = await OpenConnectionAsync(cancellationToken);

        var sisuId = await connection.QueryFirstOrDefaultAsync<long?>(usuarioSql, new { correo });
        if (sisuId is null)
        {
            return (0L, Array.Empty<object>(), Array.Empty<object>());
        }

        var asignados = await connection.QueryAsync(asignadosSql, new { sisuId });
        var sinAsignar = await connection.QueryAsync(sinAsignarSql, new { sisuId });

        return (
            sisuId.Value,
            asignados.Select(ToDictionaryObject).ToList(),
            sinAsignar.Select(ToDictionaryObject).ToList());
    }

    public async Task<string> AsignarSistemaAsync(long sisuId, IReadOnlyList<long> asignados, IReadOnlyList<long> noAsignados, CancellationToken cancellationToken)
    {
        const string mergeAssignedSql = @"
MERGE INTO AUGE_USUASISTEMA t
USING (
    SELECT :sisuId AS USUA_ID, :sistemaId AS SIST_ID
    FROM DUAL
) src
ON (t.USUA_ID = src.USUA_ID AND t.SIST_ID = src.SIST_ID)
WHEN MATCHED THEN
    UPDATE SET t.USSI_ESTADO = 1, t.USSI_FECHA = SYSDATE
WHEN NOT MATCHED THEN
    INSERT (SIST_ID, USUA_ID, USSI_ESTADO, USSI_FECHA)
    VALUES (src.SIST_ID, src.USUA_ID, 1, SYSDATE)";

        const string setUnassignedSql = @"
UPDATE AUGE_USUASISTEMA
SET USSI_ESTADO = 0,
    USSI_FECHA = SYSDATE
WHERE USUA_ID = :sisuId
  AND SIST_ID = :sistemaId";

        using var connection = await OpenConnectionAsync(cancellationToken);

        foreach (var sistemaId in asignados.Distinct())
        {
            await connection.ExecuteAsync(
                new CommandDefinition(mergeAssignedSql, new { sisuId, sistemaId }, cancellationToken: cancellationToken));
        }

        foreach (var sistemaId in noAsignados.Distinct())
        {
            await connection.ExecuteAsync(
                new CommandDefinition(setUnassignedSql, new { sisuId, sistemaId }, cancellationToken: cancellationToken));
        }

        return "Sistemas asignados correctamente";
    }

    public async Task<IReadOnlyList<object>> AllSistemasAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    SIST_ID,
    SIST_NOMBRE
FROM AUGE_SISTEMA
WHERE SIST_ESTADO = 1
ORDER BY SIST_NOMBRE";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(sql);
        return rows.Select(ToDictionaryObject).ToList();
    }

    public async Task<IReadOnlyList<object>> GetGeneralMenuTreeAsync(long sisuId, int idSistema, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    MENU_ID,
    MENU_NOMBRE,
    MENU_PADRE
FROM AUGE_MENU
WHERE MENU_ESTADO = 1
  AND MENU_SISTEMA = :idSistema
ORDER BY MENU_ID";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var menuRows = (await connection.QueryAsync(sql, new { idSistema })).ToList();

        var byId = menuRows.ToDictionary(
            row => (long)row.MENU_ID,
            row => new MenuTreeNode((long)row.MENU_ID, (string)row.MENU_NOMBRE, new List<MenuTreeNode>()));

        var roots = new List<MenuTreeNode>();

        foreach (var row in menuRows)
        {
            var current = byId[(long)row.MENU_ID];
            MenuTreeNode? parent = null;
            if (row.MENU_PADRE != null && byId.TryGetValue((long)row.MENU_PADRE, out parent) && parent != null)
            {
                parent.children.Add(current);
                continue;
            }

            roots.Add(current);
        }

        return roots.Select(node => (object)node).ToList();
    }

    public async Task<IReadOnlyList<long>> GetMenuByUserAsync(int idSistema, long sisuId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT um.MENU_ID
FROM AUGE_USUAMENU um
INNER JOIN AUGE_MENU m ON m.MENU_ID = um.MENU_ID
WHERE um.SISU_ID = :sisuId
  AND um.USME_ESTADO = 1
  AND m.MENU_ESTADO = 1
  AND m.MENU_SISTEMA = :idSistema
ORDER BY um.MENU_ID";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<long>(sql, new { sisuId, idSistema });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<long>> GetMenuUserOptionsAsync(long id, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT um.MENU_ID
FROM AUGE_USUAMENU um
INNER JOIN AUGE_MENU m ON m.MENU_ID = um.MENU_ID
WHERE um.SISU_ID = :id
  AND um.USME_ESTADO = 1
  AND m.MENU_ESTADO = 1
ORDER BY um.MENU_ID";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<long>(sql, new { id });
        return rows.ToList();
    }

    public async Task<object?> UptUserMenuAsync(long id, IReadOnlyList<long> options, int sistema, CancellationToken cancellationToken)
    {
        const string disableSql = @"
UPDATE AUGE_USUAMENU
SET USME_ESTADO = 0
WHERE SISU_ID = :id
  AND MENU_ID IN (
      SELECT MENU_ID
      FROM AUGE_MENU
      WHERE MENU_SISTEMA = :sistema
  )";

        const string mergeSelectedSql = @"
MERGE INTO AUGE_USUAMENU t
USING (
    SELECT :id AS SISU_ID, :menuId AS MENU_ID
    FROM DUAL
) src
ON (t.SISU_ID = src.SISU_ID AND t.MENU_ID = src.MENU_ID)
WHEN MATCHED THEN
    UPDATE SET t.USME_ESTADO = 1
WHEN NOT MATCHED THEN
    INSERT (USME_ID, SISU_ID, MENU_ID, USME_ESTADO)
    VALUES (SAUGE_USUAMENU.NEXTVAL, src.SISU_ID, src.MENU_ID, 1)";

        using var connection = await OpenConnectionAsync(cancellationToken);

        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(disableSql, new { id, sistema }, cancellationToken: cancellationToken));

        foreach (var menuId in options.Distinct())
        {
            rowsAffected += await connection.ExecuteAsync(
                new CommandDefinition(mergeSelectedSql, new { id, menuId }, cancellationToken: cancellationToken));
        }

        return new { rowsAffected };
    }

    private const string DuplicateEmailMessage = "El correo ya se encuentra registrado";

    private async Task<System.Data.IDbConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();

        if (connection is DbConnection dbConnection)
        {
            await dbConnection.OpenAsync(cancellationToken);
        }
        else
        {
            connection.Open();
        }

        return connection;
    }

    private object ToDictionaryObject(dynamic row)
        => ToDictionary(row);

    private static Dictionary<string, object?> ToDictionary(dynamic row)
    {
        if (row is IDictionary<string, object> dictionary)
        {
            return dictionary.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        return ((object)row)
            .GetType()
            .GetProperties()
            .ToDictionary(prop => prop.Name, prop => prop.GetValue(row));
    }

    private static long ReadLong(Dictionary<string, object?> dictionary, string key)
    {
        if (!dictionary.TryGetValue(key, out var value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            long v => v,
            int v => v,
            decimal v => (long)v,
            string v when long.TryParse(v, out var parsed) => parsed,
            _ => 0
        };
    }

    private string BuildParityJwtToken(long sisuId, int idSistema)
    {
        var headerJson = JsonSerializer.Serialize(new { alg = "HS256", typ = "JWT" });
        var payloadJson = JsonSerializer.Serialize(new
        {
            SISU_ID = sisuId,
            idSistema,
            iat = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            exp = DateTimeOffset.UtcNow.AddHours(12).ToUnixTimeSeconds()
        });

        var header = EncodeBase64Url(Encoding.UTF8.GetBytes(headerJson));
        var payload = EncodeBase64Url(Encoding.UTF8.GetBytes(payloadJson));
        var message = $"{header}.{payload}";

        var secret = configuration["Auth:JwtSecret"] ?? "veolia-auth-core-parity-secret";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var signature = EncodeBase64Url(signatureBytes);

        return $"{message}.{signature}";
    }

    private static string EncodeBase64Url(byte[] input)
        => Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private async Task<bool> EmailExistsAsync(string correo, long? excludeUserId, CancellationToken cancellationToken)
    {
        const string baseSql = @"
SELECT COUNT(1)
FROM AUGE_SISUSUARIO
WHERE LOWER(SISU_CORREO) = LOWER(:correo)";

        var sql = excludeUserId.HasValue
            ? $"{baseSql} AND SISU_ID <> :excludeUserId"
            : baseSql;

        using var connection = await OpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { correo, excludeUserId }, cancellationToken: cancellationToken));

        return count > 0;
    }

    private static string GeneratePassword(int length = 10)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        var chars = new char[length];

        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[length];
        rng.GetBytes(bytes);

        for (var i = 0; i < length; i++)
        {
            chars[i] = alphabet[bytes[i] % alphabet.Length];
        }

        return new string(chars);
    }

    private sealed record MenuTreeNode(long id, string label, List<MenuTreeNode> children);
}
