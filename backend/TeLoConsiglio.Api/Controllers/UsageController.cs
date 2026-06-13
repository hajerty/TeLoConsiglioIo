using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;

namespace TeLoConsiglio.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/usage")]
public class UsageController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IConfiguration _cfg;

    public UsageController(AppDbContext db, UserManager<ApplicationUser> users, IConfiguration cfg)
    {
        _db = db; _users = users; _cfg = cfg;
    }

    private decimal Limit()
    {
        if (!decimal.TryParse(_cfg["BUDGET_MONTHLY_USD"], System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var v))
            v = 5.0m;
        return v;
    }

    private static (DateTime start, DateTime end) MonthRangeUtc()
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        return (start, end);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var uid = _users.GetUserId(User)!;
        var (start, end) = MonthRangeUtc();

        var rows = await _db.UsageLogs
            .Where(u => u.UserId == uid && u.CreatedAt >= start && u.CreatedAt < end)
            .ToListAsync();

        var monthSpent = Math.Round(rows.Sum(r => r.EstimatedCostUsd), 6);
        var byOp = rows.GroupBy(r => r.Operation)
            .Select(g => new
            {
                op = g.Key,
                count = g.Count(),
                totalUsd = Math.Round(g.Sum(x => x.EstimatedCostUsd), 6),
                inputTokens = g.Sum(x => x.InputTokens),
                outputTokens = g.Sum(x => x.OutputTokens),
                cachedTokens = g.Sum(x => x.CachedInputTokens)
            }).OrderByDescending(x => x.totalUsd).ToList();

        var byDay = rows.GroupBy(r => r.CreatedAt.Date)
            .Select(g => new
            {
                day = g.Key.ToString("yyyy-MM-dd"),
                count = g.Count(),
                totalUsd = Math.Round(g.Sum(x => x.EstimatedCostUsd), 6)
            }).OrderBy(x => x.day).ToList();

        return Ok(new
        {
            monthSpentUsd = monthSpent,
            monthLimitUsd = Limit(),
            byOperation = byOp,
            byDay
        });
    }

    [HttpGet("admin")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Admin()
    {
        var (start, end) = MonthRangeUtc();

        var rows = await _db.UsageLogs
            .Where(u => u.CreatedAt >= start && u.CreatedAt < end)
            .ToListAsync();

        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var users = await _db.Users.Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email, u.FullName }).ToListAsync();
        var userMap = users.ToDictionary(u => u.Id, u => new { u.Email, u.FullName });

        var byUser = rows.GroupBy(r => r.UserId)
            .Select(g => new
            {
                userId = g.Key,
                email = userMap.TryGetValue(g.Key, out var u) ? u.Email : null,
                fullName = userMap.TryGetValue(g.Key, out var u2) ? u2.FullName : null,
                count = g.Count(),
                totalUsd = Math.Round(g.Sum(x => x.EstimatedCostUsd), 6),
                inputTokens = g.Sum(x => x.InputTokens),
                outputTokens = g.Sum(x => x.OutputTokens),
                cachedTokens = g.Sum(x => x.CachedInputTokens)
            }).OrderByDescending(x => x.totalUsd).ToList();

        return Ok(new
        {
            month = start.ToString("yyyy-MM"),
            monthLimitUsdPerUser = Limit(),
            totalUsd = Math.Round(rows.Sum(r => r.EstimatedCostUsd), 6),
            users = byUser
        });
    }
}
