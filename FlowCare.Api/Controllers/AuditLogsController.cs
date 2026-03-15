using System.Text;
using FlowCare.Api.Auth;
using FlowCare.Api.Data;
using FlowCare.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static FlowCare.Api.Entities.Enums;

namespace FlowCare.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _current;

    public AuditLogsController(AppDbContext db, ICurrentUser current)
    {
        _db = db;
        _current = current;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] string? actionType,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? term,
        CancellationToken ct)
    {
        if (_current.UserId is null) return Unauthorized();

        IQueryable<FlowCare.Api.Entities.AuditLog> query = _db.AuditLogs.AsNoTracking();

        // Scope
        if (_current.Role == UserRole.Admin)
        {
            // all logs
        }
        else if (_current.Role == UserRole.BranchManager)
        {
            query = query.Where(x => x.BranchId == _current.BranchId);
        }
        else
        {
            return Forbid();
        }

        // Filters
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            query = query.Where(x => x.ActionType.ToString() == actionType);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(x => x.TimestampUtc >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(x => x.TimestampUtc <= dateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x =>
                x.TargetEntityType.Contains(term) ||
                (x.TargetEntityId != null && x.TargetEntityId.Contains(term)) ||
                (x.MetadataJson != null && x.MetadataJson.Contains(term)));
        }

        var result = await query
            .OrderByDescending(x => x.TimestampUtc)
            .Select(x => new AuditLogDto
            {
                Id = x.Id,
                ActionType = x.ActionType.ToString(),
                ActorUserId = x.ActorUserId,
                ActorRole = x.ActorRole.ToString(),
                BranchId = x.BranchId,
                TargetEntityType = x.TargetEntityType,
                TargetEntityId = x.TargetEntityId,
                Timestamp = x.TimestampUtc,
                MetadataJson = x.MetadataJson
            })
            .ToListAsync(ct);

        return Ok(result);
    }

    [HttpGet("export.csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? actionType,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] string? term,
        CancellationToken ct)
    {
        if (_current.UserId is null) return Unauthorized();

        // Admin only
        if (_current.Role != UserRole.Admin)
            return Forbid();

        IQueryable<FlowCare.Api.Entities.AuditLog> query = _db.AuditLogs.AsNoTracking();

        // Filters
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            query = query.Where(x => x.ActionType.ToString() == actionType);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(x => x.TimestampUtc >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(x => x.TimestampUtc <= dateTo.Value);
        }

        if (!string.IsNullOrWhiteSpace(term))
        {
            query = query.Where(x =>
                x.TargetEntityType.Contains(term) ||
                (x.TargetEntityId != null && x.TargetEntityId.Contains(term)) ||
                (x.MetadataJson != null && x.MetadataJson.Contains(term)));
        }

        var logs = await query
            .OrderByDescending(x => x.TimestampUtc)
            .ToListAsync(ct);

        var sb = new StringBuilder();

        // CSV header
        sb.AppendLine("Id,ActionType,ActorUserId,ActorRole,BranchId,TargetEntityType,TargetEntityId,Timestamp,MetadataJson");

        foreach (var log in logs)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(log.Id.ToString()),
                EscapeCsv(log.ActionType.ToString()),
                EscapeCsv(log.ActorUserId.ToString()),
                EscapeCsv(log.ActorRole.ToString()),
                EscapeCsv(log.BranchId?.ToString() ?? ""),
                EscapeCsv(log.TargetEntityType),
                EscapeCsv(log.TargetEntityId ?? ""),
                EscapeCsv(log.TimestampUtc.ToString("O")),
                EscapeCsv(log.MetadataJson ?? "")
            ));
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", "audit-logs.csv");
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
