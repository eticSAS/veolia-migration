using Dapper;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Veolia.Api.Infrastructure.Data;

namespace Veolia.Api.Infrastructure.Auth;

public sealed class AuthJwtParityMiddleware(RequestDelegate next)
{
    private const string AccessTokenHeader = "x-access-token";
    private const string MissingTokenMessage = "No existe token de verificacion";
    private const string UnauthorizedMessage = "No Autorizado!";

    public async Task InvokeAsync(HttpContext context, IOracleConnectionFactory connectionFactory, IConfiguration configuration)
    {
        var token = context.Request.Headers[AccessTokenHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(token))
        {
            await WriteParityErrorAsync(context, StatusCodes.Status403Forbidden, MissingTokenMessage);
            return;
        }

        if (!LooksLikeJwt(token))
        {
            await WriteParityErrorAsync(context, StatusCodes.Status401Unauthorized, UnauthorizedMessage);
            return;
        }

        if (!VerifyJwtSignature(token, configuration))
        {
            await WriteParityErrorAsync(context, StatusCodes.Status401Unauthorized, UnauthorizedMessage);
            return;
        }

        if (await IsDeadTokenAsync(connectionFactory, token, context.RequestAborted))
        {
            await WriteParityErrorAsync(context, StatusCodes.Status401Unauthorized, UnauthorizedMessage);
            return;
        }

        await next(context);
    }

    private static bool LooksLikeJwt(string token)
        => token.Split('.').Length == 3;

    private static bool VerifyJwtSignature(string token, IConfiguration configuration)
    {
        var secret = configuration["Auth:JwtSecret"] ?? "veolia-auth-core-parity-secret";

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var message = $"{parts[0]}.{parts[1]}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
        var expectedSignature = EncodeBase64Url(signatureBytes);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(parts[2]));
    }

    private static string EncodeBase64Url(byte[] input)
        => Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static async Task<bool> IsDeadTokenAsync(
        IOracleConnectionFactory connectionFactory,
        string token,
        CancellationToken cancellationToken)
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

        // AS-IS parity with legacy authJwt.js: SELECT * FROM AUGE_DEADTOKEN WHERE DTKN_TOKEN = :1
        const string sql = "SELECT COUNT(1) FROM AUGE_DEADTOKEN WHERE DTKN_TOKEN = :token";

        try
        {
            var deadTokenCount = await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(sql, new { token }, cancellationToken: cancellationToken));
            return deadTokenCount > 0;
        }
        catch (Exception ex)
        {
            // Legacy environments may not have the dead-token table available.
            // Treat as not-dead rather than locking every request out.
            Console.WriteLine($"[IsDeadTokenAsync] Fallo consulta '{sql}': {ex.Message}");
            return false;
        }
    }

    private static async Task WriteParityErrorAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new { message });
        await context.Response.WriteAsync(payload);
    }
}
