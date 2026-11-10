using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VoiceKassa.Application.DTOs;
using VoiceKassa.Application.Services;

namespace VoiceKassa.Api.Controllers;

[ApiController]
[Authorize(Roles = "super-admin")]
[Route("api/super-admin")]
public class SuperAdminController : ControllerBase
{
    private readonly BusinessService _businessService;

    public SuperAdminController(BusinessService businessService) =>
        _businessService = businessService;

    [HttpGet("businesses")]
    public async Task<IActionResult> GetBusinesses(CancellationToken ct) =>
        Ok(await _businessService.GetAllBusinessesAsync(ct));

    [HttpGet("businesses/{businessId:long}/owner")]
    public async Task<IActionResult> GetOwner(long businessId, CancellationToken ct)
    {
        var owner = await _businessService.GetOwnerAdminAsync(businessId, ct);
        return owner is null ? NotFound() : Ok(owner);
    }

    [HttpPost("restaurants")]
    public async Task<IActionResult> CreateRestaurant(
        [FromBody] CreateRestaurantWithOwnerRequest request, CancellationToken ct)
    {
        var (success, error, owner) = await _businessService.CreateRestaurantWithOwnerAsync(request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }

    [HttpPost("markets")]
    public async Task<IActionResult> CreateMarket(
        [FromBody] CreateMarketWithOwnerRequest request, CancellationToken ct)
    {
        var (success, error, owner) = await _businessService.CreateMarketWithOwnerAsync(request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }

    [HttpPost("businesses")]
    public async Task<IActionResult> CreateBusiness([FromBody] CreateBusinessRequest request, CancellationToken ct) =>
        Ok(await _businessService.CreateBusinessAsync(request, ct));

    [HttpPut("businesses/{businessId:long}")]
    public async Task<IActionResult> UpdateBusiness(
        long businessId, [FromBody] UpdateBusinessRequest request, CancellationToken ct)
    {
        var (success, error, business) = await _businessService.UpdateBusinessAsync(businessId, request, ct);
        return success ? Ok(business) : BadRequest(new { error });
    }

    [HttpDelete("businesses/{businessId:long}")]
    public async Task<IActionResult> ArchiveBusiness(long businessId, CancellationToken ct)
    {
        if (!await _businessService.SetBusinessActiveAsync(businessId, false, ct))
            return NotFound(new { error = "Biznes topilmadi." });
        return NoContent();
    }

    [HttpPut("businesses/{businessId:long}/restore")]
    public async Task<IActionResult> RestoreBusiness(long businessId, CancellationToken ct)
    {
        if (!await _businessService.SetBusinessActiveAsync(businessId, true, ct))
            return NotFound(new { error = "Biznes topilmadi." });
        return NoContent();
    }

    [HttpPut("owners/status")]
    public async Task<IActionResult> UpdateOwnerStatus(
        [FromBody] UpdateOwnerStatusRequest request, CancellationToken ct)
    {
        var (success, error, owner) = await _businessService.SetOwnerActiveAsync(request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }

    [HttpPut("businesses/{businessId:long}/owner")]
    public async Task<IActionResult> UpdateOwnerDetails(
        long businessId, [FromBody] UpdateOwnerDetailsRequest request, CancellationToken ct)
    {
        var (success, error, owner) = await _businessService.UpdateOwnerDetailsAsync(businessId, request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }

    [HttpPut("owners/credentials")]
    public async Task<IActionResult> ResetOwnerCredentials(
        [FromBody] ResetOwnerCredentialsRequest request, CancellationToken ct)
    {
        var (success, error, owner) = await _businessService.ResetOwnerCredentialsAsync(request, ct);
        return success ? Ok(owner) : BadRequest(new { error });
    }
}
