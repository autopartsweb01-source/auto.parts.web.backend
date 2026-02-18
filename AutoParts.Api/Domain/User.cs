namespace AutoParts.Api.Domain;

public class User
{
    public int Id { get; set; }
    public string? Phone { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "customer";
    public string? Address { get; set; }
    public string? Location { get; set; }

    // OTP login
    public string? OtpHash { get; set; }
    public DateTime? OtpExpiry { get; set; }

    // Password / JWT
    public string? PasswordHash { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }

    public bool IsEmailConfirmed { get; set; }
    public string? EmailConfirmationToken { get; set; }
    public DateTime? EmailConfirmationExpiry { get; set; }
}
