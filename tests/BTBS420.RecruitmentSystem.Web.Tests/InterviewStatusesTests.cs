using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class InterviewStatusesTests
{
    [Theory]
    [InlineData(InterviewStatuses.Scheduled, InterviewStatuses.Completed, true)]
    [InlineData(InterviewStatuses.Scheduled, InterviewStatuses.Cancelled, true)]
    [InlineData(InterviewStatuses.Scheduled, InterviewStatuses.Scheduled, false)]
    [InlineData(InterviewStatuses.Completed, InterviewStatuses.Scheduled, false)]
    [InlineData(InterviewStatuses.Completed, InterviewStatuses.Cancelled, false)]
    [InlineData(InterviewStatuses.Cancelled, InterviewStatuses.Scheduled, false)]
    [InlineData(InterviewStatuses.Cancelled, InterviewStatuses.Completed, false)]
    public void IsValidTransition_MatrisiDogruDegerlendirir(string fromStatus, string toStatus, bool expected)
    {
        Assert.Equal(expected, InterviewStatuses.IsValidTransition(fromStatus, toStatus));
    }
}
