using System.ComponentModel.DataAnnotations;

namespace DDDExample.Application.DTOs;

public class MfaVerifyRequest
{
    [Required(ErrorMessage = "Code is required")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Code must be 6 digits")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "MFA token is required")]
    public string MfaToken { get; set; } = string.Empty;

    public bool RememberDevice { get; set; }
}