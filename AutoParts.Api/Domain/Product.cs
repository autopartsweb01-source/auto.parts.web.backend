namespace AutoParts.Api.Domain;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string SKU { get; set; }
    public decimal Price { get; set; }
    public int StockQty { get; set; }
    public string ImageSource { get; set; }
    public int PartTypeId { get; set; }
    public PartType PartType { get; set; }
}
