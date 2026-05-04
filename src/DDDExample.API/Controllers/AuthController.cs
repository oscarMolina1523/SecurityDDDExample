using DDDExample.Application.DTOs;
using DDDExample.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace DDDExample.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);

        if (result.RequiresMfa)
        {
            return Ok(new LoginResponse
            {
                RequiresMfa = true,
                MfaToken = result.MfaToken,
                User = result.User
            });
        }

        if (string.IsNullOrEmpty(result.Token))
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        return Ok(result);
    }

    [HttpPost("verify-mfa")]
    public async Task<ActionResult<LoginResponse>> VerifyMfa([FromBody] MfaVerifyRequest request)
    {
        var result = await _authService.VerifyMfaAsync(request);

        if (string.IsNullOrEmpty(result.Token))
        {
            return BadRequest(new { message = "Invalid MFA code" });
        }

        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<LoginResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var result = await _authService.RefreshTokenAsync(request);

        if (string.IsNullOrEmpty(result.Token))
        {
            return BadRequest(new { message = "Invalid refresh token" });
        }

        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponse>> Register([FromBody] RegisterRequest request)
    {
        var result = await _authService.RegisterAsync(request);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(nameof(Login), new { }, result);
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetProfile()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        // Aquí podrías obtener el usuario desde la base de datos
        return Ok(new UserDto
        {
            Id = Guid.Parse(userId),
            Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty,
            FullName = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        await _authService.RevokeUserTokensAsync(Guid.Parse(userId));
        return Ok(new { message = "Logged out successfully" });
    }

    [HttpPost("setup-mfa")]
    [Authorize]
    public async Task<ActionResult<MfaSetupResponse>> SetupMfa([FromBody] MfaSetupRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _authService.SetupMfaAsync(Guid.Parse(userId), request);

        return Ok(result);
    }

    //[HttpPost("enable-mfa")]
    //[Authorize]
    //public async Task<IActionResult> EnableMfa([FromBody] MfaVerifyRequest request)
    //{
    //    var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

    //    if (string.IsNullOrEmpty(userId))
    //        return Unauthorized();

    //    var success = await _authService.EnableMfaAsync(Guid.Parse(userId), request.Code);

    //    if (!success)
    //        return BadRequest(new { message = "Invalid MFA code" });

    //    return Ok(new { message = "MFA enabled successfully" });
    //}

    [HttpPost("enable-mfa")]
    [Authorize]
    public async Task<IActionResult> EnableMfa([FromBody] EnableMfaRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var code = request.VerificationCode.Replace(" ", "");

        var success = await _authService.EnableMfaAsync(
            Guid.Parse(userId),
            code
        );

        if (!success)
            return BadRequest(new { message = "Invalid MFA code" });

        return Ok(new
        {
            message = "MFA enabled successfully"
        });
    }

    [HttpPost("disable-mfa")]
    [Authorize]
    public async Task<IActionResult> DisableMfa()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var success = await _authService.DisableMfaAsync(Guid.Parse(userId));

        if (!success)
            return BadRequest(new { message = "Failed to disable MFA" });

        return Ok(new { message = "MFA disabled successfully" });
    }
}