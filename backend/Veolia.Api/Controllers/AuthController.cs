using Microsoft.AspNetCore.Mvc;
using Veolia.Api.Infrastructure.Auth;
using Veolia.Api.Infrastructure.Data;

namespace Veolia.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController(IAuthRepository authRepository, AuthContractMapper contractMapper) : ControllerBase
{
    [HttpPost("registro")]
    public async Task<IActionResult> Registro([FromBody] RegistroRequest request, CancellationToken cancellationToken)
    {
        var result = await authRepository.RegistroAsync(
            request.nombre,
            request.apellido,
            request.correo,
            request.password,
            request.estado,
            cancellationToken);

        if (result.IsDuplicateEmail)
        {
            return BadRequest(new { message = result.Message ?? "El correo ya se encuentra registrado" });
        }

        return Ok(result.Payload);
    }

    [HttpPost("updateUsuario")]
    public async Task<IActionResult> UpdateUsuario([FromBody] UpdateUsuarioRequest request, CancellationToken cancellationToken)
    {
        var result = await authRepository.UpdateUsuarioAsync(
            request.id,
            request.nombre,
            request.apellido,
            request.correo,
            request.estado,
            cancellationToken);

        if (result.IsDuplicateEmail)
        {
            return BadRequest(new { message = result.Message ?? "El correo ya se encuentra registrado" });
        }

        return Ok(result.Payload);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authRepository.LoginAsync(request.correo, request.pass, request.idSistema, cancellationToken);

        if (result is not LoginRepositoryResult loginResult)
        {
            return Unauthorized(contractMapper.MapLoginError(401, "Correo o contraseña inválida"));
        }

        return loginResult.Kind switch
        {
            LoginOutcomeKind.Success when loginResult.Usuario is not null && loginResult.Sistema is not null && !string.IsNullOrWhiteSpace(loginResult.AuthToken)
                => Ok(contractMapper.MapLoginSuccess(loginResult.Usuario, loginResult.AuthToken!, loginResult.Sistema, loginResult.Message)),

            LoginOutcomeKind.InvalidSystem
                => StatusCode(StatusCodes.Status404NotFound, contractMapper.MapLoginError(404, loginResult.Message)),

            LoginOutcomeKind.InvalidCredentials
                => Unauthorized(contractMapper.MapLoginError(401, loginResult.Message)),

            _ => StatusCode(StatusCodes.Status500InternalServerError, contractMapper.MapLoginError(500, "Error de autenticación"))
        };
    }

    [HttpGet("allSistemas")]
    public async Task<IActionResult> AllSistemas(CancellationToken cancellationToken)
    {
        var sistemas = await authRepository.AllSistemasAsync(cancellationToken);
        return Ok(sistemas);
    }

    [HttpGet("getSistemasByCorreo")]
    public async Task<IActionResult> GetSistemasByCorreo([FromQuery] string correo, CancellationToken cancellationToken)
    {
        try
        {
            var sistemas = await authRepository.GetSistemasByCorreoAsync(correo, cancellationToken);
            return Ok(sistemas);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = 500,
                response = ex.Message
            });
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var token = Request.Headers["x-access-token"].FirstOrDefault();
        if (!TryReadTokenContext(out var tokenContext) || string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(new { message = "No Autorizado!" });
        }

        var response = await authRepository.LogoutAsync(tokenContext.SisuId, token, cancellationToken);
        return Ok(response);
    }

    [HttpPost("getUserMenu")]
    public async Task<IActionResult> GetUserMenu(CancellationToken cancellationToken)
    {
        if (!TryReadTokenContext(out var tokenContext))
        {
            return Unauthorized(new { message = "No Autorizado!" });
        }

        var menuIds = await authRepository.GetUserMenuAsync(tokenContext.SisuId, tokenContext.IdSistema, cancellationToken);
        return Ok(contractMapper.MapUserMenuIds(menuIds));
    }

    [HttpPost("getMenuByUser")]
    public async Task<IActionResult> GetMenuByUser([FromBody] GetMenuByUserRequest request, CancellationToken cancellationToken)
    {
        var menuIds = await authRepository.GetMenuByUserAsync(request.idSistema, request.sisuId, cancellationToken);
        return Ok(menuIds);
    }

    [HttpPost("getGeneralMenuTree")]
    public async Task<IActionResult> GetGeneralMenuTree(CancellationToken cancellationToken)
    {
        if (!TryReadTokenContext(out var tokenContext))
        {
            return Unauthorized(new { message = "No Autorizado!" });
        }

        var tree = await authRepository.GetGeneralMenuTreeAsync(tokenContext.SisuId, tokenContext.IdSistema, cancellationToken);
        return Ok(tree);
    }

    [HttpPost("setChangePass")]
    public async Task<IActionResult> SetChangePass([FromBody] SetChangePassRequest request, CancellationToken cancellationToken)
    {
        if (!TryReadTokenContext(out var tokenContext))
        {
            return Unauthorized(new { message = "No Autorizado!" });
        }

        var result = await authRepository.SetChangePassAsync(
            tokenContext.SisuId,
            request.oldPass,
            request.newPass,
            request.confirmPass,
            cancellationToken);

        return StatusCode(result.Status, contractMapper.MapChangePassResponse(result.Status, result.Response, result.Msg));
    }

    [HttpGet("getAllUsers")]
    public async Task<IActionResult> GetAllUsers(CancellationToken cancellationToken)
    {
        var users = await authRepository.GetAllUsersAsync(cancellationToken);
        return Ok(users);
    }

    [HttpPost("getUserbyId")]
    public async Task<IActionResult> GetUserById([FromBody] IdRequest request, CancellationToken cancellationToken)
    {
        var users = await authRepository.GetUserByIdAsync(request.id, cancellationToken);
        return Ok(users);
    }

    [HttpPost("resetPass")]
    public async Task<IActionResult> ResetPass([FromBody] IdRequest request, CancellationToken cancellationToken)
    {
        var newPass = await authRepository.ResetPassAsync(request.id, cancellationToken);
        return Content(contractMapper.MapResetPassPlainText(newPass), "text/plain");
    }

    [HttpPost("getApsAsignadas")]
    public async Task<IActionResult> GetApsAsignadas([FromBody] IdRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var (asignadas, sinAsignar) = await authRepository.GetApsAsignadasAsync(request.id, cancellationToken);
            return Ok(contractMapper.MapApsAsignadas(asignadas, sinAsignar));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = 500,
                response = ex.Message
            });
        }
    }

    [HttpPost("setApsxUsuario")]
    public async Task<IActionResult> SetApsxUsuario([FromBody] SetApsxUsuarioRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await authRepository.SetApsxUsuarioAsync(request.id, request.outAps, request.inAps, cancellationToken);
            var mapped = contractMapper.MapSetApsxUsuarioResult(response);

            return mapped is null
                ? StatusCode(StatusCodes.Status200OK)
                : Ok(mapped);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                status = 500,
                response = ex.Message
            });
        }
    }

    [HttpPost("getSistemasPorUsuario")]
    public async Task<IActionResult> GetSistemasPorUsuario([FromBody] GetSistemasPorUsuarioRequest request, CancellationToken cancellationToken)
    {
        var (sisuId, asignados, sinAsignar) = await authRepository.GetSistemasPorUsuarioAsync(request.correo, cancellationToken);
        return Ok(new { sisuId, asignados, sinAsignar });
    }

    [HttpPost("asignarSistema")]
    public async Task<IActionResult> AsignarSistema([FromBody] AsignarSistemaRequest request, CancellationToken cancellationToken)
    {
        var message = await authRepository.AsignarSistemaAsync(request.sisuId, request.asignados, request.noAsignados, cancellationToken);
        return Ok(contractMapper.MapAsignarSistemaSuccess(message));
    }

    [HttpPost("getMenuUserOptions")]
    public async Task<IActionResult> GetMenuUserOptions([FromBody] IdRequest request, CancellationToken cancellationToken)
    {
        var menuIds = await authRepository.GetMenuUserOptionsAsync(request.id, cancellationToken);
        return Ok(menuIds);
    }

    [HttpPost("uptUserMenu")]
    public async Task<IActionResult> UptUserMenu([FromBody] UptUserMenuRequest request, CancellationToken cancellationToken)
    {
        var response = await authRepository.UptUserMenuAsync(request.id, request.options, request.sistema, cancellationToken);
        return Ok(response);
    }

    private bool TryReadTokenContext(out AuthTokenContext tokenContext)
    {
        var token = Request.Headers["x-access-token"].FirstOrDefault();
        return AuthTokenContextAccessor.TryRead(token, out tokenContext);
    }
}

public sealed record RegistroRequest(string nombre, string apellido, string correo, string password, int estado);
public sealed record UpdateUsuarioRequest(long id, string nombre, string apellido, string correo, int estado);
public sealed record LoginRequest(string correo, string pass, int idSistema);
public sealed record SetChangePassRequest(string oldPass, string newPass, string confirmPass);
public sealed record IdRequest(long id);
public sealed record SetApsxUsuarioRequest(long id, IReadOnlyList<long> outAps, IReadOnlyList<long> inAps);
public sealed record GetSistemasPorUsuarioRequest(string correo);
public sealed record AsignarSistemaRequest(long sisuId, IReadOnlyList<long> asignados, IReadOnlyList<long> noAsignados);
public sealed record GetGeneralMenuTreeRequest(int idSistema);
public sealed record GetMenuByUserRequest(int idSistema, long sisuId);
public sealed record UptUserMenuRequest(long id, IReadOnlyList<long> options, int sistema);
