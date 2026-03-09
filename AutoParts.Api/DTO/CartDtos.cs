namespace AutoParts.Api.DTO
{
    public class CartDtos
    {
        public record AddToCartRequest(int ProductId, int Qty);
        public record UpdateQtyRequest(int ProductId, int Qty);
        public record BulkUpdateRequest(List<UpdateQtyRequest> Items);

        // OCR matching
        public record OcrAddRequest(List<UpdateQtyRequest> Items);
        public class OcrMatchForm
        {
            public Microsoft.AspNetCore.Http.IFormFile? File { get; set; }
            public string? Text { get; set; }
            public int? DefaultQty { get; set; }
        }
        public record OcrMatchPreviewItem(
            int ProductId,
            string Title,
            string MatchedName,
            double Confidence,
            int RequestedQty,
            int AvailableQty,
            decimal UnitPrice
        );
        public record OcrNotFoundItem(string Name, int RequestedQty);
        public record OcrMatchResponse(List<OcrMatchPreviewItem> Items, List<OcrNotFoundItem> NotFound);

    }
}
