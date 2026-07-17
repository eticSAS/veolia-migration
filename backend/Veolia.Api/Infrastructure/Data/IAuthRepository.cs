namespace Veolia.Api.Infrastructure.Data;

public interface IAuthRepository
{
    // F-AUTH-01 Login + selección de sistema
    Task<IReadOnlyList<object>> GetSistemasByCorreoAsync(string correo, CancellationToken cancellationToken);
    Task<object?> LoginAsync(string correo, string pass, int idSistema, CancellationToken cancellationToken);

    // F-AUTH-02 Logout (dead token)
    Task<object?> LogoutAsync(long sisuId, string token, CancellationToken cancellationToken);

    // F-AUTH-03 Menú por permisos
    Task<IReadOnlyList<long>> GetUserMenuAsync(long sisuId, int idSistema, CancellationToken cancellationToken);

    // F-AUTH-04 Cambio de clave
    Task<(int Status, string Response, string Msg)> SetChangePassAsync(long sisuId, string oldPass, string newPass, string confirmPass, CancellationToken cancellationToken);

    // F-AUTH-05 CRUD usuarios + reset
    Task<IReadOnlyList<object>> GetAllUsersAsync(CancellationToken cancellationToken);
    Task<UserMutationRepositoryResult> RegistroAsync(string nombre, string apellido, string correo, string password, int estado, CancellationToken cancellationToken);
    Task<UserMutationRepositoryResult> UpdateUsuarioAsync(long id, string nombre, string apellido, string correo, int estado, CancellationToken cancellationToken);
    Task<IReadOnlyList<object>> GetUserByIdAsync(long id, CancellationToken cancellationToken);
    Task<string> ResetPassAsync(long id, CancellationToken cancellationToken);

    // F-AUTH-06 APS por usuario
    Task<(IReadOnlyList<object> Asignadas, IReadOnlyList<object> SinAsignar)> GetApsAsignadasAsync(long id, CancellationToken cancellationToken);
    Task<object?> SetApsxUsuarioAsync(long id, IReadOnlyList<long> outAps, IReadOnlyList<long> inAps, CancellationToken cancellationToken);

    // F-AUTH-07 Sistemas por usuario
    Task<(long SisuId, IReadOnlyList<object> Asignados, IReadOnlyList<object> SinAsignar)> GetSistemasPorUsuarioAsync(string correo, CancellationToken cancellationToken);
    Task<string> AsignarSistemaAsync(long sisuId, IReadOnlyList<long> asignados, IReadOnlyList<long> noAsignados, CancellationToken cancellationToken);

    // F-AUTH-08 Menú por usuario/sistema
    Task<IReadOnlyList<object>> AllSistemasAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<object>> GetGeneralMenuTreeAsync(long sisuId, int idSistema, CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> GetMenuByUserAsync(int idSistema, long sisuId, CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> GetMenuUserOptionsAsync(long id, CancellationToken cancellationToken);
    Task<object?> UptUserMenuAsync(long id, IReadOnlyList<long> options, int sistema, CancellationToken cancellationToken);
}
