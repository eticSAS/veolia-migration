using Dapper;
using System.Data.Common;

namespace Veolia.Api.Infrastructure.Data;

public sealed class ApsRepository(IOracleConnectionFactory connectionFactory) : IApsRepository
{
    public async Task<IReadOnlyList<object>> ConsultaGeneralAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    APSA_ID,
    APSA_NOMAPS,
    APSA_RESOLUCION,
    APSA_PROPIO,
    APSA_SOLORELL,
    APSA_ESTADO,
    APSA_VIAT,
    APSA_IDSUI
FROM AUCO_APSASEO
ORDER BY APSA_NOMAPS";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(sql);
        return rows.Select(ToDictionaryObject).ToList();
    }

    public async Task<IReadOnlyList<object>> ConsultaApsAsync(long apsId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    APSA_ID,
    APSA_NOMAPS,
    APSA_RESOLUCION,
    APSA_PROPIO,
    APSA_SOLORELL,
    APSA_ESTADO,
    APSA_VIAT,
    APSA_IDSUI
FROM AUCO_APSASEO
WHERE APSA_ID = :apsId";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(sql, new { apsId });
        return rows.Select(ToDictionaryObject).ToList();
    }

    public async Task<object?> CrearAsync(
        string nombre,
        int? idsui,
        int? resolucion,
        int propio,
        int relleno,
        int estado,
        int iat,
        long usuario,
        CancellationToken cancellationToken)
    {
        const string sql = @"
INSERT INTO AUCO_APSASEO (
    APSA_ID,
    APSA_NOMAPS,
    APSA_IDSUI,
    APSA_RESOLUCION,
    APSA_PROPIO,
    APSA_SOLORELL,
    APSA_ESTADO,
    APSA_VIAT,
    USUA_USUA,
    APSA_FECHACREACION
)
VALUES (
    SAUCO_APSASEO.NEXTVAL,
    :nombre,
    :idsui,
    :resolucion,
    :propio,
    :relleno,
    :estado,
    :iat,
    :usuario,
    SYSDATE
)";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new { nombre, idsui, resolucion, propio, relleno, estado, iat, usuario },
                cancellationToken: cancellationToken));

        return new { rowsAffected };
    }

    public async Task<object?> EditarAsync(
        long id,
        string nombre,
        int? idsui,
        int? resolucion,
        int propio,
        int relleno,
        int estado,
        int iat,
        CancellationToken cancellationToken)
    {
        const string sql = @"
UPDATE AUCO_APSASEO
SET APSA_NOMAPS = :nombre,
    APSA_IDSUI = :idsui,
    APSA_RESOLUCION = :resolucion,
    APSA_PROPIO = :propio,
    APSA_SOLORELL = :relleno,
    APSA_ESTADO = :estado,
    APSA_VIAT = :iat
WHERE APSA_ID = :id";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(
                sql,
                new { id, nombre, idsui, resolucion, propio, relleno, estado, iat },
                cancellationToken: cancellationToken));

        return new { rowsAffected };
    }

    public async Task<IReadOnlyList<object>> GetApsByUsuarioAsync(long sisuId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    a.APSA_ID,
    a.APSA_NOMAPS
FROM AUCO_APSUSUARIOS au
INNER JOIN AUCO_APSASEO a ON a.APSA_ID = au.APSA_ID
WHERE au.SISU_ID = :sisuId
  AND au.APSI_ESTADO = 1
  AND a.APSA_ESTADO = 1
ORDER BY a.APSA_NOMAPS";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(sql, new { sisuId });
        return rows.Select(ToDictionaryObject).ToList();
    }

    public async Task<IReadOnlyList<object>> GetUsuarioPorApsAsync(long apsId, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT
    su.SISU_CORREO,
    su.SISU_ID
FROM AUCO_APSUSUARIOS au
INNER JOIN AUGE_SISUSUARIO su ON su.SISU_ID = au.SISU_ID
WHERE au.APSA_ID = :apsId
  AND au.APSI_ESTADO = 1
  AND su.SISU_ESTADO = 1
ORDER BY su.SISU_ID";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(sql, new { apsId });
        return rows.Select(ToDictionaryObject).ToList();
    }

    public async Task<object?> EliminarAsync(long id, CancellationToken cancellationToken)
    {
        const string sql = @"
            UPDATE AUCO_APSASEO
            SET APSA_ESTADO = 0
            WHERE APSA_ID = :id";

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rowsAffected = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { id }, cancellationToken: cancellationToken));

        return new { rowsAffected };
    }

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

    private static object ToDictionaryObject(dynamic row)
    {
        if (row is IDictionary<string, object> dictionary)
        {
            return new Dictionary<string, object>(dictionary, StringComparer.OrdinalIgnoreCase);
        }

        var objectDictionary = (IDictionary<string, object>)row;
        return new Dictionary<string, object>(objectDictionary, StringComparer.OrdinalIgnoreCase);
    }
}
