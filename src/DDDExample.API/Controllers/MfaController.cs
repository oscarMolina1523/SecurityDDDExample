using DDDExample.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using DDDExample.Application.Interfaces;
using DDDExample.Domain.Entities;
using System.Text.Json;

namespace DDDExample.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MfaController : ControllerBase
{
    private readonly IMfaService _mfaService;
    private readonly UserManager<ApplicationUser> _userManager;

    public MfaController(IMfaService mfaService, UserManager<ApplicationUser> userManager)
    {
        _mfaService = mfaService;
        _userManager = userManager;
    }

    [HttpPost("setup")]
    public async Task<ActionResult<MfaSetupResponse>> SetupMfa([FromBody] MfaSetupRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        // Verificar contraseña actual
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return BadRequest(new { message = "Invalid password" });
        }

        // Generar secret y QR code
        var secret = _mfaService.GenerateSecret();
        var qrCode = _mfaService.GenerateQrCode(secret, user.Email!, "DDDExample");
        var backupCodes = _mfaService.GenerateBackupCodes();

        // Guardar configuración temporal (no habilitada hasta verificación)
        user.MfaSecret = secret;
        user.BackupCodes = JsonSerializer.Serialize(backupCodes);
        await _userManager.UpdateAsync(user);

        return Ok(new MfaSetupResponse
        {
            QrCode = qrCode,
            Secret = secret,
            BackupCodes = backupCodes,
            ManualEntryKey = FormatManualEntryKey(secret)
        });
    }

    [HttpPost("enable")]
    public async Task<ActionResult> EnableMfa([FromBody] EnableMfaRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        if (!_mfaService.VerifyToken(user.MfaSecret!, request.VerificationCode))
        {
            return BadRequest(new { message = "Invalid verification code" });
        }

        user.MfaEnabled = true;
        user.MfaSetupCompleted = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        return Ok(new { message = "MFA enabled successfully" });
    }

    [HttpPost("disable")]
    public async Task<ActionResult> DisableMfa([FromBody] DisableMfaRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Unauthorized();
        }

        // Verificar contraseña
        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return BadRequest(new { message = "Invalid password" });
        }

        // Verificar código TOTP
        if (!_mfaService.VerifyToken(user.MfaSecret!, request.VerificationCode))
        {
            return BadRequest(new { message = "Invalid verification code" });
        }

        user.MfaEnabled = false;
        user.MfaSecret = null;
        user.BackupCodes = null;
        user.MfaSetupCompleted = null;
        await _userManager.UpdateAsync(user);

        return Ok(new { message = "MFA disabled successfully" });
    }

    private string FormatManualEntryKey(string secret)
    {
        // Formatear secret para entrada manual en grupos de 4 caracteres
        return string.Join(" ", secret.Chunk(4).Select(chunk => new string(chunk.ToArray())));
    }
}

public class EnableMfaRequest
{
    public string VerificationCode { get; set; } = string.Empty;
}

public class DisableMfaRequest
{
    public string Password { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
}