namespace Veolia.Api.Infrastructure.Data;

public interface IApsRepository
{
    Task<IReadOnlyList<object>> ConsultaGeneralAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<object>> ConsultaApsAsync(long apsId, CancellationToken cancellationToken);
    Task<object?> CrearAsync(string nombre, int? idsui, int? resolucion, int propio, int relleno, int estado, int iat, long usuario, CancellationToken cancellationToken);
    Task<object?> EditarAsync(long id, string nombre, int? idsui, int? resolucion, int propio, int relleno, int estado, int iat, CancellationToken cancellationToken);
    Task<IReadOnlyList<object>> GetApsByUsuarioAsync(long sisuId, CancellationToken cancellationToken);
    Task<IReadOnlyList<object>> GetUsuarioPorApsAsync(long apsId, CancellationToken cancellationToken);
    Task<object?> EliminarAsync(long id, CancellationToken cancellationToken);
}
