namespace AutoParts.Api.Domain;

public class User
{
    public int Id { get; set; }
    public string? Phone { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Role { get; set; } = "customer";
    public string? Address { get; set; }

    // OTP login
    public string? OtpHash { get; set; }
    public DateTime? OtpExpiry { get; set; }
}
