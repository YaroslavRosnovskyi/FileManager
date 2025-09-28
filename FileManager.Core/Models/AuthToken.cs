namespace FileManager.Core.Models;

public class AuthToken
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}