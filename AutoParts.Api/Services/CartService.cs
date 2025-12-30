using AutoParts.Api.Data;
using AutoParts.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoParts.Api.Services;

public class CartService : ICartService
{
    private readonly AppDbContext _db;
    public CartService(AppDbContext db) => _db = db;

    public async Task<Cart> GetOrCreateCart(int userId)
    {
        var cart = await _db.Carts
            .Include(x => x.Items)
            .ThenInclude(i => i.Product)
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            _db.Carts.Add(cart);
            await _db.SaveChangesAsync();
        }

        return cart;
    }
    public async Task<object> GetCartSummary(int userId)
    {
        var cart = await GetOrCreateCart(userId);

        var total = cart.Items.Sum(x => x.UnitPrice * x.Qty);

        return new
        {
            cart.Id,
            items = cart.Items.Select(x => new
            {
                x.ProductId,
                x.Product.Name,
                x.Qty,
                x.UnitPrice,
                lineTotal = x.UnitPrice * x.Qty
            }),
            total
        };
    }

    public async Task<object> AddToCart(int userId, int productId, int qty)
    {
        var cart = await GetOrCreateCart(userId);

        var product = await _db.Products.FindAsync(productId);
        if (product == null)
            throw new Exception("Product not found");

        if (qty <= 0) qty = 1;

        var item = cart.Items.FirstOrDefault(x => x.ProductId == productId);

        if (item == null)
        {
            item = new CartItem
            {
                ProductId = productId,
                Qty = qty,
                UnitPrice = product.Price,
                CartId = cart.Id
            };
            cart.Items.Add(item);
        }
        else
        {
            item.Qty += qty;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await GetCartSummary(userId);
    }

    public async Task<object> DecreaseQty(int userId, int productId)
    {
        var cart = await GetOrCreateCart(userId);

        var item = cart.Items.FirstOrDefault(x => x.ProductId == productId);
        if (item == null)
            throw new Exception("Item not in cart");

        item.Qty--;

        if (item.Qty <= 0)
            _db.CartItems.Remove(item);

        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await GetCartSummary(userId);
    }

    public async Task<object> BulkUpdate(int userId, List<(int productId, int qty)> items)
    {
        var cart = await GetOrCreateCart(userId);

        foreach (var u in items)
        {
            var item = cart.Items.FirstOrDefault(x => x.ProductId == u.productId);
            if (item == null) continue;

            if (u.qty <= 0)
                _db.CartItems.Remove(item);
            else
                item.Qty = u.qty;
        }

        cart.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await GetCartSummary(userId);
    }

    public async Task<object> RemoveItem(int userId, int productId)
    {
        var cart = await GetOrCreateCart(userId);

        var item = cart.Items.FirstOrDefault(x => x.ProductId == productId);
        if (item != null)
            _db.CartItems.Remove(item);

        await _db.SaveChangesAsync();
        return await GetCartSummary(userId);
    }

    public async Task<object> ClearCart(int userId)
    {
        var cart = await GetOrCreateCart(userId);

        _db.CartItems.RemoveRange(cart.Items);
        cart.Items.Clear();

        await _db.SaveChangesAsync();
        return await GetCartSummary(userId);
    }
}
