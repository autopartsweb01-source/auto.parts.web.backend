namespace AutoParts.Api.DTO;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T? Data { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class RegisterRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string ConfirmPassword { get; set; }
    public required string Name { get; set; }
    public required string Mobile { get; set; }
    public required string Location { get; set; }
}

public class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public class RefreshTokenRequest
{
    public required string RefreshToken { get; set; }
}

public class ForgotPasswordRequest
{
    public required string Email { get; set; }
}

public class ResetPasswordRequest
{
    public required string Email { get; set; }
    public required string Token { get; set; }
    public required string NewPassword { get; set; }
}

public class AuthResponse
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public string Token { get; set; } // Spec has both "accessToken" and "token"? "token" seems redundant or same.
    public string Email { get; set; }
    public string Name { get; set; }
    public string Mobile { get; set; }
    public string Location { get; set; }
    public DateTime Expiration { get; set; }
}

public class UserProfileResponse
{
    public string Email { get; set; }
    public string Name { get; set; }
    public string Mobile { get; set; }
    public string Location { get; set; }
}

public class VerifyOtpRequestV2
{
    public string PrescriptionNo { get; set; }
    public string Otp { get; set; }
}

public class ResendOtpRequestV2
{
    public long PrescriptionNo { get; set; }
    public string OTP { get; set; }
}
