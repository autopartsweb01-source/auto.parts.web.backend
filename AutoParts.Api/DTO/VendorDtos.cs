namespace AutoParts.Api.DTO;

public class VendorCheckoutInitRequest
{
    public string PrescriptionNo { get; set; }
    public string? Phone { get; set; }
    public string? FullName { get; set; }
    public string? Address { get; set; }
}

public class VendorVerifyOtpRequest
{
    public int OrderId { get; set; }
    public string Otp { get; set; }
}
