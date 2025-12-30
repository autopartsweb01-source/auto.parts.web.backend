namespace AutoParts.Api.Domain;

public class Cart
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}

