using AutoParts.Api.Auth;
using AutoParts.Api.Data;
using AutoParts.Api.Domain;
using AutoParts.Api.DTO;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AutoParts.Api.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtTokenService _jwt;
    private readonly TwilioOtpService _otp;
    private readonly EmailService _email;

    public AuthController(AppDbContext db, JwtTokenService jwt, TwilioOtpService otp, EmailService email)
    {
        _db = db;
        _jwt = jwt;
        _otp = otp;
        _email = email;
    }

    // ---------- REGISTER ----------
    [HttpPost("register")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Register([FromBody] RegisterRequest request)
    {
        if (await _db.Users.AnyAsync(u => u.Email == request.Email))
        {
            return BadRequest(new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = "User already exists with this email",
                Errors = new List<string> { "Email already registered" }
            });
        }

        var user = new User
        {
            Email = request.Email,
            FullName = request.Name,
            Phone = request.Mobile,
            Location = request.Location,
            Role = "customer",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var accessToken = _jwt.Generate(user);
        var refreshToken = _jwt.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1); // Spec says 1 day
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<AuthResponse>
        {
            Success = true,
            Message = "User registered successfully",
            Data = new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Token = accessToken,
                Email = user.Email,
                Name = user.FullName,
                Mobile = user.Phone,
                Location = user.Location,
                Expiration = DateTime.UtcNow.AddMinutes(60)
            }
        });
    }

    // ---------- LOGIN ----------
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> Login([FromBody] LoginRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null || user.PasswordHash == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return BadRequest(new ApiResponse<AuthResponse> // Spec uses 200 OK with success: false or 400? Spec implies failure response structure. Usually 400 or 401. Spec failure example looks like 200 OK structure but "success": false. I will return Ok with success false to match typical "200 OK" style if implied, but typically APIs return 400/401. I'll use 400 BadRequest for failure to be safe standard-wise, or Ok if they want 200. I'll use Ok with Success=false as per many "enterprise" APIs.
            {
                Success = false,
                Message = "Invalid credentials",
                Errors = new List<string> { "Invalid email or password" }
            });
        }

        var accessToken = _jwt.Generate(user);
        var refreshToken = _jwt.GenerateRefreshToken();

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<AuthResponse>
        {
            Success = true,
            Message = "Login successful",
            Data = new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Token = accessToken,
                Email = user.Email,
                Name = user.FullName,
                Mobile = user.Phone,
                Location = user.Location,
                Expiration = DateTime.UtcNow.AddMinutes(60)
            }
        });
    }

    // ---------- REFRESH TOKEN ----------
    [HttpPost("refresh-token")]
    public async Task<ActionResult<ApiResponse<AuthResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.RefreshToken == request.RefreshToken);
        
        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
             return BadRequest(new ApiResponse<AuthResponse>
            {
                Success = false,
                Message = "Invalid or expired refresh token",
                Errors = new List<string> { "Invalid token" }
            });
        }

        var newAccessToken = _jwt.Generate(user);
        var newRefreshToken = _jwt.GenerateRefreshToken();

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1);
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<AuthResponse>
        {
            Success = true,
            Message = "Token refreshed successfully",
            Data = new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken, // Spec shows token refreshed
                Token = newAccessToken,
                Email = user.Email,
                Name = user.FullName,
                Mobile = user.Phone,
                Location = user.Location,
                Expiration = DateTime.UtcNow.AddMinutes(60)
            }
        });
    }

    // ---------- FORGOT PASSWORD ----------
    // POST /api/forgot-password (Note: Route is at root /api/forgot-password based on spec, but this controller is /auth. I will add [Route] override)
    [HttpPost("/api/forgot-password")]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
        if (user == null)
        {
            return Ok(new ApiResponse<object>
            {
                Success = false,
                Message = "Email not found"
            });
        }

        var resetToken = Guid.NewGuid().ToString();
        user.ResetToken = resetToken;
        user.ResetTokenExpiry = DateTime.UtcNow.AddMinutes(15);
        await _db.SaveChangesAsync();

        // Send email with token
        await _email.SendOtpEmailAsync(user.Email, $"Your reset token is: {resetToken}");

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Reset token sent to email"
        });
    }

    // ---------- RESET PASSWORD ----------
    [HttpPost("/api/reset-password")]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email && u.ResetToken == request.Token);
        if (user == null || user.ResetTokenExpiry < DateTime.UtcNow)
        {
             return Ok(new ApiResponse<object>
            {
                Success = false,
                Message = "Invalid or expired token"
            });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.ResetToken = null;
        user.ResetTokenExpiry = null;
        await _db.SaveChangesAsync();

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Password reset successful"
        });
    }

    // Keep existing OTP endpoints for backward compatibility or if needed, but mapped to /auth/send-otp
    private static string NormalizePhone(string phone)
    {
        phone = phone.Trim();
        return phone.StartsWith("+91") ? phone : $"+91{phone}";
    }

    [HttpPost("send-otp")]
    public async Task<IActionResult> SendOtp([FromBody] SendOtpRequest request)
    {
        // ... (Keep existing implementation if desired, or remove if strictly following spec. I'll keep it)
        var phone = NormalizePhone(request.Phone);
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Phone == phone);
        if (user == null)
        {
            user = new User
            {
                Phone = phone,
                FullName = "User",
                Role = "customer",
                Email = request.Email ?? ""
            };
            _db.Users.Add(user);
        }
        else if (!string.IsNullOrEmpty(request.Email))
        {
            user.Email = request.Email;
        }

        var otp = new Random().Next(100000, 999999).ToString();
        user.OtpHash = BCrypt.Net.BCrypt.HashPassword(otp);
        user.OtpExpiry = DateTime.UtcNow.AddMinutes(5);
        await _db.SaveChangesAsync();
        await _otp.SendSmsOtpAsync(phone, otp);
        if (!string.IsNullOrEmpty(user.Email)) await _email.SendOtpEmailAsync(user.Email, otp);

        return Ok(new { message = "OTP sent successfully" });
    }
}
