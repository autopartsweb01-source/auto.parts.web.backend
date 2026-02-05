namespace AutoParts.Api.Domain;

public class User
{
    public int Id { get; set; }
    public string? Phone { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Role { get; set; } = "customer";
    public string? Address { get; set; }

    // OTP login (Legacy/Alternative)
    public string? OtpHash { get; set; }
    public DateTime? OtpExpiry { get; set; }

    // Password & Refresh Token
    public string? PasswordHash { get; set; }
    public string? Location { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }
}
