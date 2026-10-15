using BudgetBoard.Service.Interfaces;
using BudgetBoard.WebAPI.Resources;
using Microsoft.Extensions.Localization;
using Quartz;

namespace BudgetBoard.WebAPI.Jobs;

[DisallowConcurrentExecution]
public class RenewalAlertJob(
    ILogger<RenewalAlertJob> logger,
    IRenewalAlertService renewalAlertService,
    IStringLocalizer<ApiLogStrings> logLocalizer
) : IJob
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken)
    {
        logger.LogInformation("{LogMessage}", logLocalizer["RenewalAlertJobStartLog"]);
        try
        {
            var sent = await renewalAlertService.SendDueAlertsAsync(cancellationToken);
            logger.LogInformation("{LogMessage}", logLocalizer["RenewalAlertJobCompleteLog", sent]);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{LogMessage}", logLocalizer["RenewalAlertJobErrorLog"]);
        }
    }
}
