using BTBS420.RecruitmentSystem.Web.Models;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class ApplicationStatusesTests
{
    [Theory]
    [InlineData(ApplicationStatuses.New, ApplicationStatuses.Withdrawn, true)]
    [InlineData(ApplicationStatuses.Screening, ApplicationStatuses.Withdrawn, true)]
    [InlineData(ApplicationStatuses.Interview, ApplicationStatuses.Withdrawn, true)]
    [InlineData(ApplicationStatuses.Withdrawn, ApplicationStatuses.New, false)]
    [InlineData(ApplicationStatuses.Withdrawn, ApplicationStatuses.Withdrawn, false)]
    [InlineData(ApplicationStatuses.New, ApplicationStatuses.Interview, false)]
    [InlineData(ApplicationStatuses.New, ApplicationStatuses.New, false)]
    [InlineData(ApplicationStatuses.Screening, ApplicationStatuses.Rejected, true)]
    [InlineData(ApplicationStatuses.Interview, ApplicationStatuses.Rejected, true)]
    [InlineData(ApplicationStatuses.New, ApplicationStatuses.Rejected, false)]
    [InlineData(ApplicationStatuses.Rejected, ApplicationStatuses.Screening, true)]
    [InlineData(ApplicationStatuses.Rejected, ApplicationStatuses.Interview, false)]
    [InlineData(ApplicationStatuses.Rejected, ApplicationStatuses.Withdrawn, false)]
    [InlineData(ApplicationStatuses.Rejected, ApplicationStatuses.Rejected, false)]
    public void IsValidTransition_MatrisiDogruDegerlendirir(string fromStatus, string toStatus, bool expected)
    {
        Assert.Equal(expected, ApplicationStatuses.IsValidTransition(fromStatus, toStatus));
    }

    [Theory]
    [InlineData(ApplicationStatuses.New, true)]
    [InlineData(ApplicationStatuses.Screening, true)]
    [InlineData(ApplicationStatuses.Interview, true)]
    [InlineData(ApplicationStatuses.Withdrawn, false)]
    public void CanWithdraw_GeriCekilebilirDurumlariDogruBelirler(string status, bool expected)
    {
        Assert.Equal(expected, ApplicationStatuses.CanWithdraw(status));
    }
}
