namespace AutoParts.Api.Domain;

public class Product
{
    public int Id { get; set; }
    public string Title { get; set; }
    public decimal Price { get; set; }
    public string Tag { get; set; }
    public string Category { get; set; }
    public string ImageDataUrl { get; set; }
    public int Quantity { get; set; }

    // Keeping these for DB compatibility or future use if needed, but primary is above
    public string? SKU { get; set; }
    public int? PartTypeId { get; set; }
    public PartType? PartType { get; set; }
}
