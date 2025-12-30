namespace AutoParts.Api.DTO
{
    public class VerifyOtpRequest
    {
        public required string Phone { get; set; }
        public required string Code { get; set; }
    }
}
