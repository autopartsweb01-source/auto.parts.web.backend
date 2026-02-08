using AutoParts.Api.Data;
using AutoParts.Api.DTO;
using AutoParts.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutoParts.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    // ---------- GET OWN PROFILE (By Token) ----------
    [HttpGet("/user/profile")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileResponse>>> GetOwnProfile()
    {
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrEmpty(email))
        {
             // Fallback for some JWT configurations where "email" claim is used instead of ClaimTypes.Email
             email = User.FindFirst("email")?.Value;
        }

        if (string.IsNullOrEmpty(email))
            return Unauthorized(new ApiResponse<object> { Success = false, Message = "Invalid token" });

        var user = await _userService.GetUserByEmailAsync(email);
        if (user == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "User not found" });

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
        var user = await _userService.GetUserByEmailAsync(email);
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

    // ---------- ADMIN: GET ALL USERS WITH PAGING ----------
    [HttpGet("list")]
    public async Task<IActionResult> GetAllUsers(string? search, int page = 1, int size = 10)
    {
        var (items, total) = await _userService.GetAllUsersAsync(search, page, size);

        var resultItems = items.Select(u => new 
        {
            u.Id,
            u.FullName,
            u.Email,
            u.Phone,
            u.Role,
            u.Address,
            u.Location
        });

        return Ok(new { items = resultItems, total, page, size });
    }

    // ---------- ADMIN: DELETE USER ----------
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var success = await _userService.DeleteUserAsync(id);
        if (!success) return NotFound(new { message = "User not found" });

        return Ok(new { message = "User deleted successfully" });
    }

    // ---------- ADMIN: BULK DELETE USERS ----------
    [HttpPost("delete-bulk")]
    public async Task<IActionResult> DeleteUsers([FromBody] List<int> ids)
    {
        var count = await _userService.DeleteUsersAsync(ids);
        return Ok(new { message = $"{count} users deleted successfully" });
    }
}
