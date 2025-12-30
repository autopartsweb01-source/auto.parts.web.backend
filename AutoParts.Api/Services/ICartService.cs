using AutoParts.Api.Domain;

namespace AutoParts.Api.Services;

public interface ICartService
{
    Task<Cart> GetOrCreateCart(int userId);
    Task<object> GetCartSummary(int userId);
    Task<object> AddToCart(int userId, int productId, int qty);
    Task<object> DecreaseQty(int userId, int productId);
    Task<object> BulkUpdate(int userId, List<(int productId, int qty)> items);
    Task<object> RemoveItem(int userId, int productId);
    Task<object> ClearCart(int userId);
}

