namespace AutoParts.Api.Domain;

public class User
{
    public int Id { get; set; }
    public string? PhoneNumber { get; set; }
    public string Name { get; set; }
    public string Role { get; set; } = "User";
    public string? Address { get; set; }

    // OTP login
    public string? OtpHash { get; set; }
    public DateTime? OtpExpiry { get; set; }
}
