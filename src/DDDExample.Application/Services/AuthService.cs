using DDDExample.Application.DTOs;
using DDDExample.Application.Interfaces;
using DDDExample.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace DDDExample.Application.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
        {
            return null;
        }

        user.UpdateLastLogin();
        await _userManager.UpdateAsync(user);

        var token = _tokenService.GenerateToken(user);

        return new LoginResponse
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt
            }
        };
    }

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return new RegisterResponse
            {
                Success = false,
                Message = "Email already exists"
            };
        }

        var user = new ApplicationUser(request.Email, request.FirstName, request.LastName);

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return new RegisterResponse
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            };
        }

        return new RegisterResponse
        {
            Success = true,
            Message = "User registered successfully",
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                CreatedAt = user.CreatedAt
            }
        };
    }
}