using AutoParts.Api.Data;
using AutoParts.Api.Domain;
using AutoParts.Api.DTO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace AutoParts.Api.Controllers;

[ApiController]
[Route("api/user")]
public class UserController : ControllerBase
{
    private readonly AppDbContext _db;

    public UserController(AppDbContext db)
    {
        _db = db;
    }

    // ---------- GET LOGGED-IN USER PROFILE ----------
    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> GetProfile()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
             // Try to get by ID if email is missing (legacy flow) or phone
             // But spec implies Email is key.
             var userId = User.FindFirst("id")?.Value;
             if (userId != null) 
             {
                 var userById = await _db.Users.FindAsync(int.Parse(userId));
                 if (userById != null)
                 {
                      return Ok(new ApiResponse<UserProfileResponse>
                      {
                        Success = true,
                        Data = new UserProfileResponse
                        {
                            Email = userById.Email,
                            Name = userById.FullName,
                            Mobile = userById.Phone,
                            Location = userById.Location
                        }
                      });
                 }
             }
             return Unauthorized(new ApiResponse<object> { Success = false, Message = "Unauthorized" });
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Unauthorized" });
        }

        return Ok(new ApiResponse<UserProfileResponse>
        {
            Success = true,
            Data = new UserProfileResponse
            {
                Email = user.Email,
                Name = user.FullName,
                Mobile = user.Phone,
                Location = user.Location
            }
        });
    }

    // ---------- GET PROFILE BY EMAIL ----------
    [HttpGet("profile/{email}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> GetProfileByEmail(string email)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            return NotFound(new ApiResponse<object> { Success = false, Message = "User not found" });
        }

        return Ok(new ApiResponse<UserProfileResponse>
        {
            Success = true,
            Data = new UserProfileResponse
            {
                Email = user.Email,
                Name = user.FullName,
                Mobile = user.Phone,
                Location = user.Location
            }
        });
    }
}
