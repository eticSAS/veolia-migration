using Microsoft.AspNetCore.Mvc;
using Veolia.Api.Infrastructure.Aps;
using Veolia.Api.Infrastructure.Auth;
using Veolia.Api.Infrastructure.Data;

namespace Veolia.Api.Controllers;

[ApiController]
[Route("api/v1/aps")]
public sealed class ApsController(
    IApsRepository apsRepository,
    ApsContractMapper contractMapper,
    ILogger<ApsController> logger) : ControllerBase
{
    [HttpPost("consultageneral")]
    public async Task<IActionResult> ConsultaGeneral(CancellationToken cancellationToken)
    {
        try
        {
            var data = await apsRepository.ConsultaGeneralAsync(cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en Aps.ConsultaGeneral");
            return StatusCode(StatusCodes.Status500InternalServerError, contractMapper.MapLegacyError());
        }
    }

    [HttpPost("consultaaps")]
    public async Task<IActionResult> ConsultaAps([FromBody] ApsByIdRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var data = await apsRepository.ConsultaApsAsync(request.aps, cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en Aps.ConsultaAps para aps {Aps}", request.aps);
            return StatusCode(StatusCodes.Status500InternalServerError, contractMapper.MapLegacyError());
        }
    }

    [HttpPost("crear")]
    public async Task<IActionResult> Crear([FromBody] ApsMutationRequest request, CancellationToken cancellationToken)
    {
        if (!TryReadTokenContext(out var tokenContext))
            return Unauthorized(new { message = "No Autorizado!" });

        try
        {
            var result = await apsRepository.CrearAsync(
                request.nombre,
                request.idsui,
                request.resolucion,
                request.propio,
                request.relleno,
                request.estado,
                request.iat,
                tokenContext.SisuId,
                cancellationToken);

            return Ok(contractMapper.MapMutationResponse(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en Aps.Crear para nombre {Nombre}", request.nombre);
            return StatusCode(StatusCodes.Status500InternalServerError, contractMapper.MapLegacyError());
        }
    }

    [HttpPut("editar/{id:long}")]
    public async Task<IActionResult> Editar(long id, [FromBody] ApsMutationRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await apsRepository.EditarAsync(
                id,
                request.nombre,
                request.idsui,
                request.resolucion,
                request.propio,
                request.relleno,
                request.estado,
                request.iat,
                cancellationToken);

            return Ok(contractMapper.MapMutationResponse(result));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en Aps.Editar para id {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, contractMapper.MapLegacyError());
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetApsByUsuario(CancellationToken cancellationToken)
    {
        if (!TryReadTokenContext(out var tokenContext))
        {
            return Unauthorized(new { message = "No Autorizado!" });
        }

        try
        {
            var data = await apsRepository.GetApsByUsuarioAsync(tokenContext.SisuId, cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en Aps.GetApsByUsuario para usuario {SisuId}", tokenContext.SisuId);
            return StatusCode(StatusCodes.Status500InternalServerError, contractMapper.MapLegacyError());
        }
    }

    [HttpPost("usuarioPorAPS")]
    public async Task<IActionResult> UsuarioPorAps([FromBody] ApsByIdRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var data = await apsRepository.GetUsuarioPorApsAsync(request.aps, cancellationToken);
            return Ok(data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en Aps.UsuarioPorAps para aps {Aps}", request.aps);
            return StatusCode(StatusCodes.Status500InternalServerError, contractMapper.MapLegacyError());
        }
    }

    [HttpDelete("eliminar/{id:long}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken cancellationToken)
    {
        if (!TryReadTokenContext(out _))
            return Unauthorized(new { message = "No Autorizado!" });

        try
        {
            var result = await apsRepository.EliminarAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error en Aps.Eliminar para id {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError, contractMapper.MapLegacyError());
        }
    }

    private bool TryReadTokenContext(out AuthTokenContext tokenContext)
    {
        var token = Request.Headers["x-access-token"].FirstOrDefault();
        return AuthTokenContextAccessor.TryRead(token, out tokenContext);
    }
}

public sealed record ApsByIdRequest(long aps);

public sealed record ApsMutationRequest(
    string nombre,
    int? idsui,
    int? resolucion,
    int propio,
    int relleno,
    int estado,
    int iat);
