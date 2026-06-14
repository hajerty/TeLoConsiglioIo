using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeLoConsiglio.Domain.Entities;
using TeLoConsiglio.Infrastructure.Data;
using TeLoConsiglio.Infrastructure.Services;

namespace TeLoConsiglio.Api.Controllers;

public record AuditLogDto(
    Guid Id,
    string? UserId,
    string? UserEmail,
    string Action,
    string? Resource,
    string? DetailsJson,
    string? IpAddress,
    string? UserAgent,
    DateTime CreatedAt);

[ApiController]
[Authorize(Policy = "RequireAdminOnly")]
[Route("api/admin/audit-log")]
public class AuditLogController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditLogger _audit;

    public AuditLogController(AppDbContext db, IAuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    /// <summary>
    /// Lista audit log con filtri, paginazione. Header X-Total-Count.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AuditLogDto>>> List(
        [FromQuery] string? userId,
        [FromQuery] string? action,
        [FromQuery] string? q,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (pageSize > 200) pageSize = 200;
        if (page < 1) page = 1;

        IQueryable<AuditLog> query = _db.AuditLogs;

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(a => a.UserId == userId);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(a =>
                a.Action.ToLower().Contains(ql) ||
                (a.Resource != null && a.Resource.ToLower().Contains(ql)));
        }

        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value.ToUniversalTime());

        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value.ToUniversalTime());

        var total = await query.CountAsync();
        Response.Headers["X-Total-Count"] = total.ToString();

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        try { await _audit.LogAsync("admin.audit-log.view"); } catch { }

        return Ok(items.Select(ToDto).ToList());
    }

    /// <summary>
    /// Esporta audit log in CSV RFC 4180. Max 50000 righe. Stesso set di filtri (no paginazione).
    /// </summary>
    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? userId,
        [FromQuery] string? action,
        [FromQuery] string? q,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        IQueryable<AuditLog> query = _db.AuditLogs;

        if (!string.IsNullOrWhiteSpace(userId))
            query = query.Where(a => a.UserId == userId);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var ql = q.ToLower();
            query = query.Where(a =>
                a.Action.ToLower().Contains(ql) ||
                (a.Resource != null && a.Resource.ToLower().Contains(ql)));
        }

        if (from.HasValue)
            query = query.Where(a => a.CreatedAt >= from.Value.ToUniversalTime());

        if (to.HasValue)
            query = query.Where(a => a.CreatedAt <= to.Value.ToUniversalTime());

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Take(50_000)
            .ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("id,userId,userEmail,action,resource,details,ipAddress,userAgent,createdAt");
        foreach (var item in items)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                CsvEscape(item.Id.ToString()),
                CsvEscape(item.UserId),
                CsvEscape(item.UserEmail),
                CsvEscape(item.Action),
                CsvEscape(item.Resource),
                CsvEscape(item.DetailsJson),
                CsvEscape(item.IpAddress),
                CsvEscape(item.UserAgent),
                CsvEscape(item.CreatedAt.ToString("o"))
            }));
        }

        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var fileName = $"audit-log-{today}.csv";

        try { await _audit.LogAsync("admin.audit-log.export-csv"); } catch { }

        return File(
            Encoding.UTF8.GetBytes(sb.ToString()),
            "text/csv",
            fileName);
    }

    private static AuditLogDto ToDto(AuditLog a) =>
        new(a.Id, a.UserId, a.UserEmail, a.Action, a.Resource, a.DetailsJson, a.IpAddress, a.UserAgent, a.CreatedAt);

    // RFC 4180: wrappa in virgolette se il campo contiene virgola, newline o virgoletta;
    // raddoppia le virgolette interne.
    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
