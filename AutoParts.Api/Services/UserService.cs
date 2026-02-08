using AutoParts.Api.Data;
using AutoParts.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;

    public UserService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<User?> GetUserByEmailAsync(string email)
    {
        return await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
    }

    public async Task<(List<User> Items, int Total)> GetAllUsersAsync(string? search, int page, int size)
    {
        var query = _db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(u => 
                (u.FullName != null && u.FullName.ToLower().Contains(search)) ||
                (u.Email != null && u.Email.ToLower().Contains(search)) ||
                (u.Phone != null && u.Phone.Contains(search))
            );
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(u => u.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return (items, total);
    }

    public async Task<bool> DeleteUserAsync(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null) return false;

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> DeleteUsersAsync(List<int> ids)
    {
        if (ids == null || !ids.Any()) return 0;

        var users = await _db.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
        if (users.Any())
        {
            _db.Users.RemoveRange(users);
            await _db.SaveChangesAsync();
        }
        return users.Count;
    }
}
