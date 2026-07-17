using Dapper;
using Oracle.ManagedDataAccess.Client;
using Veolia.Api.Infrastructure.Data.Interfaces;

namespace Veolia.Api.Infrastructure.Data;

public sealed class InfoGerencialRepository(IOracleConnectionFactory connectionFactory) : IInfoGerencialRepository
{
    public Task<IReadOnlyList<object>> GetDetalleCostosAsync(int anno, int mes, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT A.APSA_NOMAPS, C.*
              FROM VAUCO_COSTOS C
              INNER JOIN AUCO_APSASEO A ON (C.APSCOSTO = A.APSA_ID)
             WHERE C.ANNOCOSTO = :1
               AND C.MESCOSTO = :2";

        return QueryRowsAsync(sql, [anno, mes], cancellationToken);
    }

    public Task<IReadOnlyList<object>> GetDetalleSubAporteAsync(int anno, int mes, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT S.*, P.PARA_NOMBRE
              FROM VAUCO_SUBSAPORT S
              INNER JOIN AUGE_PARAMETROS P ON (S.PARA_TIPPRED20016 = P.PARA_PARA AND P.CLAS_CLAS = 20016)
             WHERE S.SUCO_ANNO = :1
               AND S.SUCO_MES = :2
               AND S.SUCO_ESTADO = 1";

        return QueryRowsAsync(sql, [anno, mes], cancellationToken);
    }

    public Task<IReadOnlyList<object>> GetInfoApsEmprDiviAsync(int aps, int anno, int mes, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT AE.EMPR_NOMBRE, AD.DIVI_NOMBRE, AI.*
              FROM AUCO_INFOAPSEMPRDIVI AI
              JOIN AUGE_EMPRESAS AE ON AE.EMPR_EMPR = AI.EMPR_EMPR
              JOIN AUGE_DIVIPOLI AD ON AD.DIVI_DIVI = AI.DIVI_DIVI
             WHERE AI.APSA_ID = :1
               AND AI.IAED_ANNO = :2
               AND AI.IAED_MES = :3
             ORDER BY AE.EMPR_NOMBRE, AD.DIVI_NOMBRE";

        return QueryRowsAsync(sql, [aps, anno, mes], cancellationToken);
    }

    public Task<IReadOnlyList<object>> GetInfoEmprDiviAsync(int aps, int anno, int mes, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT E.EMPR_NOMBRE, I.*
              FROM AUCO_INFOEMPRDIVI I
              INNER JOIN AUCO_APSEMPRDIVI D ON D.AEDI_ID = I.AEDI_ID
              INNER JOIN AUGE_EMPRESAS E ON E.EMPR_EMPR = D.EMPR_EMPR
             WHERE I.APSA_ID = :1
               AND I.IEDI_ANNO = :2
               AND I.IEDI_MES = :3
             ORDER BY E.EMPR_NOMBRE";

        return QueryRowsAsync(sql, [aps, anno, mes], cancellationToken);
    }

    public Task<IReadOnlyList<object>> GetInfoApsRellenoAsync(int aps, int anno, int mes, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT R.RELL_NOMBRE, I.*
              FROM AUCO_INFOAPSRELLENO I
              INNER JOIN AUCO_RELLENOS R ON R.RELL_RELL = I.RELL_RELL
             WHERE I.APSA_ID = :1
               AND I.IARE_ANNO = :2
               AND I.IARE_MES = :3
               AND R.RELL_ESTADO = 1
             ORDER BY R.RELL_NOMBRE";

        return QueryRowsAsync(sql, [aps, anno, mes], cancellationToken);
    }

    public Task<IReadOnlyList<object>> GetDashBoardGerencialAsync(int anno, int mes, long usuario, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT APS.APSA_ID,
                   APS.APSA_NOMAPS,
                   TAR.TARI_ANNO,
                   TAR.TARI_MES,
                   TAR.TARI_ASEO,
                   TAR.TARI_ACUE,
                   SU.SISU_ID,
                   SU.SISU_NOMBRES
              FROM AUCO_APSASEO APS
              INNER JOIN AUCO_TARIFAS TAR ON TAR.APSA_ID = APS.APSA_ID
              INNER JOIN AUCO_APSUSUARIOS AU ON AU.APSA_ID = APS.APSA_ID
              INNER JOIN AUGE_SISUSUARIO SU ON SU.SISU_ID = AU.SISU_ID
             WHERE TAR.TARI_ANNO = :1
               AND TAR.TARI_MES = :2
               AND AU.SISU_ID = :3
             ORDER BY APS.APSA_NOMAPS";

        return QueryRowsAsync(sql, [anno, mes, usuario], cancellationToken);
    }

    public Task<IReadOnlyList<object>> GetCostoPodaAsync(int aps, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT *
              FROM VPODA_REPORTE
             WHERE APSA_ID = :1
             ORDER BY PERIODO DESC";

        return QueryRowsAsync(sql, [aps], cancellationToken);
    }

    private async Task<IReadOnlyList<object>> QueryRowsAsync(string sql, object[] values, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        for (var i = 0; i < values.Length; i++)
        {
            parameters.Add((i + 1).ToString(), values[i]);
        }

        using var connection = await OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync(sql, parameters);
        return rows.Select(ToDictionaryObject).ToList();
    }

    private async Task<OracleConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = connectionFactory.CreateConnection();
        if (connection is not OracleConnection oracleConnection)
        {
            throw new InvalidOperationException("OracleConnectionFactory must return OracleConnection.");
        }

        if (oracleConnection.State != System.Data.ConnectionState.Open)
        {
            await oracleConnection.OpenAsync(cancellationToken);
        }

        return oracleConnection;
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
