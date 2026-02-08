namespace AutoParts.Api.DTO;

public class UserProfileResponse
{
    public string Email { get; set; }
    public string Name { get; set; }
    public string Mobile { get; set; }
    public string? Location { get; set; }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; }
    public T? Data { get; set; }
}
