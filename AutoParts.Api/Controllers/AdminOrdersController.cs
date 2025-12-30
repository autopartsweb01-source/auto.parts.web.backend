using AutoParts.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("admin/orders")]
[Authorize(Roles = "Admin")]
public class AdminOrdersController : ControllerBase
{
    private readonly IAdminOrderService _svc;

    public AdminOrdersController(IAdminOrderService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    public async Task<IActionResult> List(string? status, int page = 1, int size = 20)
        => Ok(await _svc.GetAllOrders(status, page, size));

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> Approve(int id)
        => Ok(await _svc.Approve(id));

    [HttpPost("{id}/out-for-delivery")]
    public async Task<IActionResult> OutForDelivery(int id)
        => Ok(await _svc.MarkOutForDelivery(id));

    [HttpPost("{id}/generate-otp")]
    public async Task<IActionResult> GenOtp(int id)
        => Ok(await _svc.GenerateDeliveryOtp(id));

    [HttpPost("{id}/verify-otp")]
    public async Task<IActionResult> VerifyOtp(int id, string otp)
        => Ok(await _svc.VerifyDeliveryOtp(id, otp));

    [HttpPost("{id}/deliver")]
    public async Task<IActionResult> Deliver(int id)
        => Ok(await _svc.MarkDelivered(id));

    [HttpPost("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id, string reason)
        => Ok(await _svc.CancelOrder(id, reason));
}

