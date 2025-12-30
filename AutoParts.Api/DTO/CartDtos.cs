namespace AutoParts.Api.DTO
{
    public class CartDtos
    {
        public record AddToCartRequest(int ProductId, int Qty);
        public record UpdateQtyRequest(int ProductId, int Qty);
        public record BulkUpdateRequest(List<UpdateQtyRequest> Items);

    }
}
