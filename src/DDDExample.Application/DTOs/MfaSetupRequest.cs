using System.ComponentModel.DataAnnotations;

namespace DDDExample.Application.DTOs;

public class MfaSetupRequest
{
    [Required(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}