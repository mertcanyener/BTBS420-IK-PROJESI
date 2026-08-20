using BTBS420.RecruitmentSystem.Web.ActivityLogging;
using BTBS420.RecruitmentSystem.Web.Authorization;
using BTBS420.RecruitmentSystem.Web.Data;
using BTBS420.RecruitmentSystem.Web.Models;
using BTBS420.RecruitmentSystem.Web.ViewModels.ActivityLogs;
using BTBS420.RecruitmentSystem.Web.ViewModels.Positions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class ActivityLogsController(ApplicationDbContext dbContext) : Controller
{
    private const int DefaultPageSize = 25;

    private const int MaximumPageSize = 100;

    private static readonly string[] CsvHeaders =
    [
        "Tarih (UTC)", "Aktör", "İşlem", "Hedef Türü", "Hedef Kimliği", "İlan", "Aday", "Özet"
    ];

    [HttpGet]
    public async Task<IActionResult> Index(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? userId,
        int? jobPostingId,
        string? candidateId,
        string? actionCode,
        int page = 1,
        int pageSize = DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaximumPageSize);

        var query = BuildFilteredQuery(dateFrom, dateTo, userId, jobPostingId, candidateId, actionCode);

        var totalCount = await query.CountAsync(cancellationToken);
        var pagedLogs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var entries = await BuildListItemsAsync(pagedLogs, cancellationToken);

        var filter = new ActivityLogFilterViewModel(
            dateFrom, dateTo, userId, jobPostingId, candidateId, actionCode, page, pageSize);
        var filterOptions = await BuildFilterOptionsAsync(cancellationToken);

        return View(new ActivityLogIndexViewModel(entries, filter, filterOptions, totalCount));
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? userId,
        int? jobPostingId,
        string? candidateId,
        string? actionCode,
        CancellationToken cancellationToken)
    {
        var query = BuildFilteredQuery(dateFrom, dateTo, userId, jobPostingId, candidateId, actionCode);
        var logs = await query.ToListAsync(cancellationToken);
        var entries = await BuildListItemsAsync(logs, cancellationToken);

        var rows = entries.Select(
            entry => (IReadOnlyList<string?>)
            [
                entry.OccurredAtUtc.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                entry.ActorName,
                entry.ActionLabel,
                entry.TargetEntityType,
                entry.TargetEntityId,
                entry.JobPostingTitle,
                entry.CandidateName,
                entry.Summary
            ]);

        var csvBytes = CsvExportHelper.BuildCsv(CsvHeaders, rows);
        var fileName = $"aktivite-kayitlari-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

        return File(csvBytes, "text/csv", fileName);
    }

    private IQueryable<ActivityLog> BuildFilteredQuery(
        DateOnly? dateFrom,
        DateOnly? dateTo,
        string? userId,
        int? jobPostingId,
        string? candidateId,
        string? actionCode)
    {
        var query = dbContext.ActivityLogs.AsQueryable();

        if (dateFrom.HasValue)
        {
            var fromUtc = dateFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(log => log.OccurredAtUtc >= fromUtc);
        }

        if (dateTo.HasValue)
        {
            var toUtc = dateTo.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(log => log.OccurredAtUtc <= toUtc);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(log => log.ActorUserId == userId);
        }

        if (jobPostingId.HasValue)
        {
            var jobPostingIdText = jobPostingId.Value.ToString();
            query = query.Where(log => log.JobPostingId == jobPostingIdText);
        }

        if (!string.IsNullOrWhiteSpace(candidateId))
        {
            query = query.Where(log => log.CandidateId == candidateId);
        }

        if (!string.IsNullOrWhiteSpace(actionCode) && ActivityActionCodes.IsDefined(actionCode))
        {
            query = query.Where(log => log.ActionCode == actionCode);
        }

        return query.OrderByDescending(log => log.OccurredAtUtc);
    }

    private async Task<IReadOnlyList<ActivityLogListItemViewModel>> BuildListItemsAsync(
        IReadOnlyList<ActivityLog> logs,
        CancellationToken cancellationToken)
    {
        var actorIds = logs
            .Where(log => log.ActorUserId is not null)
            .Select(log => log.ActorUserId!)
            .Distinct()
            .ToList();

        var actorNames = await dbContext.Users
            .Where(user => actorIds.Contains(user.Id))
            .Select(user => new { user.Id, user.UserName })
            .ToDictionaryAsync(
                user => user.Id, user => user.UserName ?? string.Empty, cancellationToken);

        var jobPostingIds = logs
            .Where(log => log.JobPostingId is not null)
            .Select(log => int.TryParse(log.JobPostingId, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var jobPostingTitles = await dbContext.JobPostings
            .Where(jobPosting => jobPostingIds.Contains(jobPosting.Id))
            .Select(jobPosting => new { jobPosting.Id, jobPosting.Title })
            .ToDictionaryAsync(
                jobPosting => jobPosting.Id, jobPosting => jobPosting.Title, cancellationToken);

        var candidateIds = logs
            .Where(log => log.CandidateId is not null)
            .Select(log => log.CandidateId!)
            .Distinct()
            .ToList();

        var candidateNames = await dbContext.CandidateProfiles
            .Where(profile => candidateIds.Contains(profile.ApplicationUserId))
            .Select(
                profile => new
                {
                    profile.ApplicationUserId,
                    FullName = profile.FirstName + " " + profile.LastName
                })
            .ToDictionaryAsync(
                profile => profile.ApplicationUserId, profile => profile.FullName, cancellationToken);

        return logs
            .Select(
                log => new ActivityLogListItemViewModel(
                    log.Id,
                    log.OccurredAtUtc,
                    log.ActorUserId,
                    log.ActorUserId is not null && actorNames.TryGetValue(log.ActorUserId, out var actorName)
                        ? actorName
                        : "-",
                    ActivityActionCodes.GetDisplayLabel(log.ActionCode),
                    log.TargetEntityType,
                    log.TargetEntityId,
                    log.JobPostingId is not null &&
                        int.TryParse(log.JobPostingId, out var jobPostingId) &&
                        jobPostingTitles.TryGetValue(jobPostingId, out var jobPostingTitle)
                        ? jobPostingTitle
                        : null,
                    log.CandidateId is not null &&
                        candidateNames.TryGetValue(log.CandidateId, out var candidateName)
                        ? candidateName
                        : null,
                    log.Summary))
            .ToList();
    }

    private async Task<ActivityLogFilterOptionsViewModel> BuildFilterOptionsAsync(
        CancellationToken cancellationToken)
    {
        var userOptions = await dbContext.Users
            .OrderBy(user => user.UserName)
            .Select(user => new TextSelectOptionViewModel(user.Id, user.UserName ?? user.Id))
            .ToListAsync(cancellationToken);

        var jobPostingOptions = await dbContext.JobPostings
            .OrderBy(jobPosting => jobPosting.Title)
            .Select(jobPosting => new SelectOptionViewModel(jobPosting.Id, jobPosting.Title))
            .ToListAsync(cancellationToken);

        var candidateOptions = await dbContext.CandidateProfiles
            .OrderBy(profile => profile.FirstName)
            .ThenBy(profile => profile.LastName)
            .Select(
                profile => new TextSelectOptionViewModel(
                    profile.ApplicationUserId, profile.FirstName + " " + profile.LastName))
            .ToListAsync(cancellationToken);

        var actionCodeOptions = ActivityActionCodes.All
            .OrderBy(code => ActivityActionCodes.GetDisplayLabel(code), StringComparer.Ordinal)
            .ToList();

        return new ActivityLogFilterOptionsViewModel(
            userOptions, jobPostingOptions, candidateOptions, actionCodeOptions);
    }
}
