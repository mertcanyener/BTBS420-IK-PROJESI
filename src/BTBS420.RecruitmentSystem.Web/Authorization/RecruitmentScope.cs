using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.Authorization;

public sealed class RecruitmentScope
{
    public static RecruitmentScope Unrestricted { get; } = new(true, null, null);

    private RecruitmentScope(bool isUnrestricted, string? responsibleUserId, int? departmentId)
    {
        IsUnrestricted = isUnrestricted;
        ResponsibleUserId = responsibleUserId;
        DepartmentId = departmentId;
    }

    public bool IsUnrestricted { get; }

    public string? ResponsibleUserId { get; }

    public int? DepartmentId { get; }

    public static RecruitmentScope ForResponsibleUser(string responsibleUserId)
    {
        return new RecruitmentScope(false, responsibleUserId, null);
    }

    public static RecruitmentScope ForDepartment(int? departmentId)
    {
        return new RecruitmentScope(false, null, departmentId);
    }

    public IQueryable<JobPosting> ApplyToJobPostings(IQueryable<JobPosting> query)
    {
        if (IsUnrestricted)
        {
            return query;
        }

        if (ResponsibleUserId is not null)
        {
            return query.Where(jobPosting => jobPosting.ResponsibleUserId == ResponsibleUserId);
        }

        return DepartmentId is null
            ? query.Where(_ => false)
            : query.Where(jobPosting => jobPosting.Position.DepartmentId == DepartmentId.Value);
    }

    public IQueryable<JobApplication> ApplyToJobApplications(IQueryable<JobApplication> query)
    {
        if (IsUnrestricted)
        {
            return query;
        }

        if (ResponsibleUserId is not null)
        {
            return query.Where(application => application.JobPosting.ResponsibleUserId == ResponsibleUserId);
        }

        return DepartmentId is null
            ? query.Where(_ => false)
            : query.Where(
                application => application.JobPosting.Position.DepartmentId == DepartmentId.Value);
    }

    public IQueryable<Interview> ApplyToInterviews(IQueryable<Interview> query)
    {
        if (IsUnrestricted)
        {
            return query;
        }

        if (ResponsibleUserId is not null)
        {
            return query.Where(
                interview => interview.JobApplication.JobPosting.ResponsibleUserId == ResponsibleUserId);
        }

        return DepartmentId is null
            ? query.Where(_ => false)
            : query.Where(
                interview =>
                    interview.JobApplication.JobPosting.Position.DepartmentId == DepartmentId.Value);
    }

    public IQueryable<Offer> ApplyToOffers(IQueryable<Offer> query)
    {
        if (IsUnrestricted)
        {
            return query;
        }

        if (ResponsibleUserId is not null)
        {
            return query.Where(
                offer => offer.JobApplication.JobPosting.ResponsibleUserId == ResponsibleUserId);
        }

        return DepartmentId is null
            ? query.Where(_ => false)
            : query.Where(
                offer =>
                    offer.JobApplication.JobPosting.Position.DepartmentId == DepartmentId.Value);
    }

    public bool Includes(JobPosting jobPosting)
    {
        if (IsUnrestricted)
        {
            return true;
        }

        if (ResponsibleUserId is not null)
        {
            return jobPosting.ResponsibleUserId == ResponsibleUserId;
        }

        return DepartmentId is not null && jobPosting.Position.DepartmentId == DepartmentId.Value;
    }

    public bool Includes(Interview interview)
    {
        return Includes(interview.JobApplication.JobPosting);
    }
}
