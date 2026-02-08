using AutoParts.Api.Domain;
using AutoParts.Api.DTO;

namespace AutoParts.Api.Services;

public interface IUserService
{
    Task<User?> GetUserByEmailAsync(string email);
    Task<(List<User> Items, int Total)> GetAllUsersAsync(string? search, int page, int size);
    Task<bool> DeleteUserAsync(int id);
    Task<int> DeleteUsersAsync(List<int> ids);
}
