using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using test_project.Models.DTO;
using test_project.Repositories.Interfaces;
using test_project.Services.Interfaces;
using test_project.Auth;
using test_project.Models.Entities;

namespace test_project.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository userRepository,
        IApiKeyRepository apiKeyRepository,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _userRepository = userRepository;
        _apiKeyRepository = apiKeyRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<TokenResponseDto?> LoginAsync(LoginDto loginDto)
    {
        _logger.LogInformation("Попытка входа пользователя: {Username}", loginDto.Username);

        var user = await _userRepository.GetByUsernameAsync(loginDto.Username);
        if (user == null)
        {
            _logger.LogWarning("Пользователь не найден: {Username}", loginDto.Username);
            return null;
        }

        if (!VerifyPassword(loginDto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Неверный пароль для пользователя: {Username}", loginDto.Username);
            return null;
        }

        var secretKey = _configuration["Jwt:SecretKey"] ?? "your-super-secret-key-change-this-in-production-minimum-32-characters-long";
        var token = JwtHelper.GenerateToken(user.Id, user.Role, secretKey, 60);

        _logger.LogInformation("Успешный вход пользователя: {Username}, роль: {Role}", loginDto.Username, user.Role);

        return new TokenResponseDto
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            Role = user.Role
        };
    }

    public async Task<TokenResponseDto?> RegisterAsync(RegisterDto registerDto)
    {
        _logger.LogInformation("Регистрация нового пользователя: {Username}", registerDto.Username);

        var existingUser = await _userRepository.GetByUsernameAsync(registerDto.Username);
        if (existingUser != null)
        {
            _logger.LogWarning("Пользователь уже существует: {Username}", registerDto.Username);
            return null;
        }

        var existingEmail = await _userRepository.GetByEmailAsync(registerDto.Email);
        if (existingEmail != null)
        {
            _logger.LogWarning("Email уже используется: {Email}", registerDto.Email);
            return null;
        }

        var passwordHash = HashPassword(registerDto.Password);
        var user = new User
        {
            Username = registerDto.Username,
            Email = registerDto.Email,
            PasswordHash = passwordHash,
            Role = "User"
        };

        await _userRepository.CreateAsync(user);

        var secretKey = _configuration["Jwt:SecretKey"] ?? "your-super-secret-key-change-this-in-production-minimum-32-characters-long";
        var token = JwtHelper.GenerateToken(user.Id, user.Role, secretKey, 60);

        _logger.LogInformation("Пользователь успешно зарегистрирован: {Username}", registerDto.Username);

        return new TokenResponseDto
        {
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            Role = user.Role
        };
    }

    public async Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        var key = await _apiKeyRepository.GetByKeyAsync(apiKey);
        return key != null;
    }

    public string GenerateJwtToken(int userId, string role)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "your-super-secret-key-change-this-in-production-minimum-32-characters-long";
        return JwtHelper.GenerateToken(userId, role, secretKey, 60);
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private static bool VerifyPassword(string password, string passwordHash)
    {
        var hashOfInput = HashPassword(password);
        return hashOfInput == passwordHash;
    }
}
