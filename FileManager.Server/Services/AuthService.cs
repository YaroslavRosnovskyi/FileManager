using FileManager.Core.Interfaces;
using FileManager.Core.Models;
using System.Security.Cryptography;
using System.Text;

namespace FileManager.Server.Services;

public class AuthService : IAuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly string _userStoragePath;
    private readonly Dictionary<string, AuthToken> _activeTokens = new();
    private readonly List<UserAccount> _users = new();

    public AuthService(ILogger<AuthService> logger, IConfiguration configuration)
    {
        _logger = logger;
        var storagePath = configuration["StoragePaths:Base"] ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Storage");
        _userStoragePath = Path.Combine(storagePath, "Users");
        
        if (!Directory.Exists(_userStoragePath))
        {
            Directory.CreateDirectory(_userStoragePath);
        }

        string salt = GenerateSalt();
        _users.Add(new UserAccount
        {
            Id = "1",
            Username = "demo",
            Email = "demo@example.com",
            Salt = salt,
            PasswordHash = HashPassword("password", salt),
            CreatedAt = DateTime.UtcNow.AddDays(-30),
            LastLoginAt = DateTime.UtcNow.AddDays(-1)
        });
    }

    public async Task<(bool Success, string Token, string ErrorMessage)> LoginAsync(string username, string password)
    {
        await Task.CompletedTask;

        try
        {
            var user = _users.FirstOrDefault(u => u.Username == username);
            if (user == null)
            {
                return (false, string.Empty, "User not found");
            }

            if (HashPassword(password, user.Salt) != user.PasswordHash)
            {
                return (false, string.Empty, "Invalid password");
            }

            var token = new AuthToken
            {
                Token = Guid.NewGuid().ToString(),
                UserId = user.Id,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            _activeTokens[token.Token] = token;
            
            user.LastLoginAt = DateTime.UtcNow;

            _logger.LogInformation("User {Username} logged in successfully", username);
            return (true, token.Token, string.Empty);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for user {Username}", username);
            return (false, string.Empty, $"Login error: {ex.Message}");
        }
    }

    public async Task<bool> LogoutAsync(string token)
    {
        await Task.CompletedTask;

        if (_activeTokens.ContainsKey(token))
        {
            _activeTokens.Remove(token);
            return true;
        }

        return false;
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        await Task.CompletedTask;

        if (_activeTokens.TryGetValue(token, out var authToken))
        {
            if (authToken.IsExpired)
            {
                _activeTokens.Remove(token);
                return false;
            }
            return true;
        }

        return false;
    }

    public async Task<UserAccount?> GetUserFromTokenAsync(string token)
    {
        await Task.CompletedTask;

        if (!await ValidateTokenAsync(token))
        {
            return null;
        }

        var authToken = _activeTokens[token];
        return _users.FirstOrDefault(u => u.Id == authToken.UserId);
    }

    private string GenerateSalt()
    {
        byte[] saltBytes = new byte[16];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(saltBytes);
        }
        return Convert.ToBase64String(saltBytes);
    }

    private string HashPassword(string password, string salt)
    {
        using var sha256 = SHA256.Create();
        var passwordWithSalt = password + salt;
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(passwordWithSalt));
        return Convert.ToBase64String(hashBytes);
    }
}