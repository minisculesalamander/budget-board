using BudgetBoard.Database.Models;
using BudgetBoard.Service.Interfaces;
using BudgetBoard.Service.Models;
using BudgetBoard.WebAPI.Resources;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace BudgetBoard.WebAPI.Controllers;

[Route("api/envelopes")]
[ApiController]
public class EnvelopeBudgetController(
    ILogger<EnvelopeBudgetController> logger,
    UserManager<ApplicationUser> userManager,
    IEnvelopeBudgetService envelopeBudgetService,
    IStringLocalizer<ApiLogStrings> logLocalizer,
    IStringLocalizer<ApiResponseStrings> responseLocalizer
) : ApiControllerBase<EnvelopeBudgetController>(logger, logLocalizer, responseLocalizer)
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Read(DateOnly month)
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            return Ok(await envelopeBudgetService.ReadMonthAsync(userId, month));
        });
    }

    [HttpPut("enabled")]
    [Authorize]
    public async Task<IActionResult> SetEnabled([FromBody] EnvelopeEnabledRequest request)
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            await envelopeBudgetService.SetEnabledAsync(userId, request.Enabled);
            return Ok();
        });
    }

    [HttpPut("assign")]
    [Authorize]
    public async Task<IActionResult> Assign([FromBody] EnvelopeAssignRequest request)
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            await envelopeBudgetService.SetAssignedAsync(userId, request);
            return Ok();
        });
    }

    [HttpPost("move")]
    [Authorize]
    public async Task<IActionResult> Move([FromBody] EnvelopeMoveRequest request)
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            await envelopeBudgetService.MoveAsync(userId, request);
            return Ok();
        });
    }

    [HttpPut("accounts")]
    [Authorize]
    public async Task<IActionResult> SetAccounts([FromBody] EnvelopeAccountsRequest request)
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            await envelopeBudgetService.SetOnBudgetAccountsAsync(userId, request.AccountIds);
            return Ok();
        });
    }

    private bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var raw = userManager.GetUserId(User);
        return !string.IsNullOrWhiteSpace(raw) && Guid.TryParse(raw, out userId);
    }
}
