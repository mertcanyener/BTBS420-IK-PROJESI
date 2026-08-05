namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class Offer
{
    public const int MaximumNoteLength = 2000;

    private Offer()
    {
    }

    internal Offer(
        int jobApplicationId,
        string createdByUserId,
        decimal salary,
        DateOnly startDate,
        string? note,
        DateTime createdAtUtc)
    {
        JobApplicationId = jobApplicationId;
        CreatedByUserId = NormalizeCreatedByUserId(createdByUserId);
        Salary = NormalizeSalary(salary);
        StartDate = startDate;
        Note = NormalizeNote(note);
        Status = OfferStatuses.Draft;
        CreatedAtUtc = createdAtUtc;
    }

    public int Id { get; private set; }

    public int JobApplicationId { get; private set; }

    public JobApplication JobApplication { get; private set; } = null!;

    public string Status { get; private set; } = OfferStatuses.Draft;

    public decimal Salary { get; private set; }

    public DateOnly StartDate { get; private set; }

    public string? Note { get; private set; }

    public string CreatedByUserId { get; private set; } = string.Empty;

    public ApplicationUser CreatedByUser { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    internal void EditDraft(decimal salary, DateOnly startDate, string? note)
    {
        if (Status != OfferStatuses.Draft)
        {
            throw new InvalidOperationException(
                $"'{OfferStatuses.GetDisplayLabel(Status)}' durumundaki bir teklif düzenlenemez.");
        }

        Salary = NormalizeSalary(salary);
        StartDate = startDate;
        Note = NormalizeNote(note);
    }

    internal OfferStatusChange Submit(string actorUserId, DateTime changedAtUtc)
    {
        if (Salary <= 0 || StartDate == default)
        {
            throw new InvalidOperationException("Eksik bilgiler nedeniyle teklif gönderilemez.");
        }

        return TransitionTo(OfferStatuses.PendingManagerApproval, actorUserId, reason: null, changedAtUtc);
    }

    internal OfferStatusChange Approve(string actorUserId, DateTime changedAtUtc)
    {
        return TransitionTo(OfferStatuses.Approved, actorUserId, reason: null, changedAtUtc);
    }

    internal OfferStatusChange Reject(string actorUserId, string reason, DateTime changedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Red gerekçesi boş olamaz.", nameof(reason));
        }

        return TransitionTo(OfferStatuses.RejectedByManager, actorUserId, reason, changedAtUtc);
    }

    internal OfferStatusChange Accept(string actorUserId, DateTime changedAtUtc)
    {
        return TransitionTo(OfferStatuses.Accepted, actorUserId, reason: null, changedAtUtc);
    }

    internal OfferStatusChange RejectByCandidate(string actorUserId, DateTime changedAtUtc)
    {
        return TransitionTo(OfferStatuses.RejectedByCandidate, actorUserId, reason: null, changedAtUtc);
    }

    private OfferStatusChange TransitionTo(
        string newStatus,
        string actorUserId,
        string? reason,
        DateTime changedAtUtc)
    {
        if (!OfferStatuses.IsValidTransition(Status, newStatus))
        {
            throw new InvalidOperationException(
                $"'{OfferStatuses.GetDisplayLabel(Status)}' durumundaki bir teklif " +
                $"'{OfferStatuses.GetDisplayLabel(newStatus)}' durumuna geçemez.");
        }

        var change = new OfferStatusChange(Id, Status, newStatus, actorUserId, reason, changedAtUtc);
        Status = newStatus;
        return change;
    }

    private static string NormalizeCreatedByUserId(string createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(createdByUserId))
        {
            throw new ArgumentException(
                "Teklifi oluşturan kullanıcı kimliği boş olamaz.",
                nameof(createdByUserId));
        }

        return createdByUserId;
    }

    private static decimal NormalizeSalary(decimal salary)
    {
        if (salary <= 0)
        {
            throw new ArgumentException("Maaş sıfırdan büyük olmalıdır.", nameof(salary));
        }

        return salary;
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var normalized = note.Trim();

        if (normalized.Length > MaximumNoteLength)
        {
            throw new ArgumentException(
                $"Not en fazla {MaximumNoteLength} karakter olabilir.",
                nameof(note));
        }

        return normalized;
    }
}
