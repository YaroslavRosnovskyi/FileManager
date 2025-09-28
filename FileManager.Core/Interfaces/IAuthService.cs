using FileManager.Core.Models;

namespace FileManager.Core.Interfaces;

public interface IAuthService
{
    Task<(bool Success, string Token, string ErrorMessage)> LoginAsync(string username, string password);
    Task<bool> LogoutAsync(string token);
    Task<bool> ValidateTokenAsync(string token);
    Task<UserAccount?> GetUserFromTokenAsync(string token);
}