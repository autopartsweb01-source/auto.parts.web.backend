using AutoParts.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("chat")]
public class ChatController : ControllerBase
{
    private readonly IChatService _chat;

    public ChatController(IChatService chat)
    {
        _chat = chat;
    }

    private int UserId() =>
        int.Parse(User.Claims.First(x => x.Type == "id").Value);

    // ---------------- USER CHAT ----------------

    [Authorize(Roles = "User")]
    [HttpPost("send")]
    public async Task<IActionResult> UserSend(string message)
    {
        var uid = UserId();
        return Ok(await _chat.SendMessage(uid, uid, false, message));
    }

    [Authorize(Roles = "User")]
    [HttpGet("me")]
    public async Task<IActionResult> MyChat(int page = 1, int size = 20)
        => Ok(await _chat.GetConversation(UserId(), page, size));

    // ---------------- ADMIN CHAT ----------------

    [Authorize(Roles = "Admin")]
    [HttpPost("admin/send")]
    public async Task<IActionResult> AdminSend(int userId, string message)
        => Ok(await _chat.SendMessage(userId, UserId(), true, message));

    // List users with recent chat
    [Authorize(Roles = "Admin")]
    [HttpGet("admin/users")]
    public async Task<IActionResult> ChatUsers(int page = 1, int size = 20)
        => Ok(await _chat.GetUserListWithLatestMessage(page, size));

    // View chat with specific user
    [Authorize(Roles = "Admin")]
    [HttpGet("admin/conversation")]
    public async Task<IActionResult> AdminConversation(int userId, int page = 1, int size = 20)
        => Ok(await _chat.GetConversation(userId, page, size));
}

