using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;

namespace TeLoConsiglio.Api.Auth;

/// <summary>
/// Filtra le chiamate ai controller/azioni AI: blocca con 429 se l'utente ha
/// superato il budget mensile (BUDGET_MONTHLY_USD, default 5.0 USD).
/// A 80% del budget aggiunge header X-Budget-Warning e logga warning.
/// </summary>
public class BudgetGuardAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var sp = context.HttpContext.RequestServices;
        var db = sp.GetRequiredService<AppDbContext>();
        var users = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var cfg = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("BudgetGuard");

        var uid = users.GetUserId(context.HttpContext.User);
        if (string.IsNullOrEmpty(uid))
        {
            await next();
            return;
        }

        if (!decimal.TryParse(cfg["BUDGET_MONTHLY_USD"], System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var limit))
            limit = 5.0m;

        var now = DateTime.UtcNow;
        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var spent = await db.UsageLogs
            .Where(u => u.UserId == uid && u.CreatedAt >= startOfMonth)
            .SumAsync(u => (decimal?)u.EstimatedCostUsd) ?? 0m;

        if (spent >= limit)
        {
            logger.LogWarning("Budget mensile superato per user {Uid}: spent={Spent} limit={Limit}", uid, spent, limit);
            context.Result = new ObjectResult(new
            {
                error = "budget_exceeded",
                spentUsd = Math.Round(spent, 4),
                limitUsd = Math.Round(limit, 4)
            })
            {
                StatusCode = StatusCodes.Status429TooManyRequests
            };
            return;
        }

        if (spent >= limit * 0.8m)
        {
            logger.LogWarning("Budget mensile all'80% per user {Uid}: spent={Spent} limit={Limit}", uid, spent, limit);
            context.HttpContext.Response.Headers["X-Budget-Warning"] = "80%";
        }

        await next();
    }
}
