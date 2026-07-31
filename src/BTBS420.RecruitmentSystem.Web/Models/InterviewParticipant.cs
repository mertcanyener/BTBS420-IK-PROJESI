namespace BTBS420.RecruitmentSystem.Web.Models;

public sealed class InterviewParticipant
{
    private InterviewParticipant()
    {
    }

    internal InterviewParticipant(int interviewId, string participantUserId, DateTime assignedAtUtc)
    {
        InterviewId = interviewId;
        ParticipantUserId = NormalizeParticipantUserId(participantUserId);
        AssignedAtUtc = assignedAtUtc;
    }

    public int Id { get; private set; }

    public int InterviewId { get; private set; }

    public Interview Interview { get; private set; } = null!;

    public string ParticipantUserId { get; private set; } = string.Empty;

    public ApplicationUser ParticipantUser { get; private set; } = null!;

    public DateTime AssignedAtUtc { get; private set; }

    private static string NormalizeParticipantUserId(string participantUserId)
    {
        if (string.IsNullOrWhiteSpace(participantUserId))
        {
            throw new ArgumentException("Katılımcı kimliği boş olamaz.", nameof(participantUserId));
        }

        return participantUserId;
    }
}
