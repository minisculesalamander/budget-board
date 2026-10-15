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
public class YearPlanController(
    ILogger<YearPlanController> logger,
    UserManager<ApplicationUser> userManager,
    IYearPlanService yearPlanService,
    IStringLocalizer<ApiLogStrings> logLocalizer,
    IStringLocalizer<ApiResponseStrings> responseLocalizer
) : ApiControllerBase<YearPlanController>(logger, logLocalizer, responseLocalizer)
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Read(int startYear, int yearCount = 1)
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            return Ok(await yearPlanService.ReadPlanAsync(userId, startYear, yearCount));
        });
    }

    [HttpPut("settings")]
    [Authorize]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] PlanningSettingsUpdateRequest request
    )
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            await yearPlanService.UpdateSettingsAsync(userId, request);
            return Ok();
        });
    }

    [HttpPut("cell")]
    [Authorize]
    public async Task<IActionResult> UpsertCell([FromBody] YearPlanCellUpdateRequest request)
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            await yearPlanService.UpsertCellAsync(userId, request);
            return Ok();
        });
    }

    [HttpPost("copy")]
    [Authorize]
    public async Task<IActionResult> Copy(int fromYear)
    {
        return await HandleRequestAsync(async () =>
        {
            if (!TryGetUserId(out var userId))
            {
                return Unauthorized();
            }

            await yearPlanService.CopyYearAsync(userId, fromYear);
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
