using test_project.Models.DTO;

namespace test_project.Services.Interfaces;

public interface IAuthService
{
    Task<TokenResponseDto?> LoginAsync(LoginDto loginDto);
    Task<TokenResponseDto?> RegisterAsync(RegisterDto registerDto);
    Task<bool> ValidateApiKeyAsync(string apiKey);
    string GenerateJwtToken(int userId, string role);
}
