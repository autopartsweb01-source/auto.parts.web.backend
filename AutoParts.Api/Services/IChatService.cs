namespace AutoParts.Api.Services;

public interface IChatService
{
    Task<object> SendMessage(int userId, int senderId, bool isAdmin, string message);
    Task<object> GetConversation(int userId, int page, int size);
    Task<object> GetUserListWithLatestMessage(int page, int size);
}

