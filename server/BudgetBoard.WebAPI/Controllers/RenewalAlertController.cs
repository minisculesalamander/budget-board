using BudgetBoard.Database.Models;
using BudgetBoard.Service.Interfaces;
using BudgetBoard.Service.Models;
using BudgetBoard.WebAPI.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace BudgetBoard.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RenewalAlertController(
    ILogger<RenewalAlertController> logger,
    UserManager<ApplicationUser> userManager,
    IRenewalAlertService renewalAlertService,
    IStringLocalizer<ApiLogStrings> logLocalizer,
    IStringLocalizer<ApiResponseStrings> responseLocalizer
) : ApiControllerBase<RenewalAlertController>(logger, logLocalizer, responseLocalizer)
{
    [HttpGet("settings")]
    [Authorize]
    public async Task<IActionResult> ReadSettings()
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            return Ok(await renewalAlertService.ReadSettingsAsync(userId));
        });
    }

    [HttpPut("settings")]
    [Authorize]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] RenewalAlertSettingsUpdateRequest request
    )
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            return Ok(await renewalAlertService.UpdateSettingsAsync(userId, request));
        });
    }

    [HttpPost("test")]
    [Authorize]
    public async Task<IActionResult> SendTest(CancellationToken cancellationToken)
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            await renewalAlertService.SendTestAsync(userId, cancellationToken);
            return Ok();
        });
    }

    [HttpGet("preferences")]
    [Authorize]
    public async Task<IActionResult> ReadPreferences()
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            return Ok(await renewalAlertService.ReadPreferencesAsync(userId));
        });
    }

    [HttpPut("preferences/{recurringRuleId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdatePreference(
        Guid recurringRuleId,
        [FromBody] SubscriptionRenewalPreferenceUpdateRequest request
    )
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            return Ok(
                await renewalAlertService.UpdatePreferenceAsync(userId, recurringRuleId, request)
            );
        });
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = userManager.GetUserId(User);
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out userId);
    }
}
