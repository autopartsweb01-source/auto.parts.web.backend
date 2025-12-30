using AutoParts.Api.Data;
using AutoParts.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Services;

public class ChatService : IChatService
{
    private readonly AppDbContext _db;

    public ChatService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<object> SendMessage(int userId, int senderId, bool isAdmin, string message)
    {
        var msg = new ChatMessage
        {
            UserId = userId,
            SenderUserId = senderId,
            IsAdmin = isAdmin,
            Message = message
        };

        _db.ChatMessages.Add(msg);
        await _db.SaveChangesAsync();

        return msg;
    }

    public async Task<object> GetConversation(int userId, int page, int size)
    {
        var q = _db.ChatMessages
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.SentAt);

        var total = await q.CountAsync();

        var msgs = await q.Skip((page - 1) * size)
            .Take(size)
            .OrderBy(x => x.SentAt)
            .ToListAsync();

        return new { page, size, total, messages = msgs };
    }

    // For admin — list users with latest message
    public async Task<object> GetUserListWithLatestMessage(int page, int size)
    {
        var q =
            from m in _db.ChatMessages
            group m by m.UserId
            into g
            let last = g.OrderByDescending(x => x.SentAt).First()
            orderby last.SentAt descending
            select new
            {
                UserId = g.Key,
                LastMessage = last.Message,
                last.SentAt,
                last.IsAdmin
            };

        var total = await q.CountAsync();

        var list = await q.Skip((page - 1) * size).Take(size).ToListAsync();

        return new { page, size, total, list };
    }
}
