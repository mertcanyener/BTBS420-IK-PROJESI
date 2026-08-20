using BTBS420.RecruitmentSystem.Web.Ai;
using BTBS420.RecruitmentSystem.Web.Ai.Evaluation;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class EvaluationEvidenceGroundingValidatorTests
{
    [Fact]
    public void EnsureGrounded_GecerliKanitListesi_HataFirlatmaz()
    {
        var evidence = new[]
        {
            new EvaluationEvidenceItem(
                Claim: "Yönetici deneyimi mevcut.",
                SourceField: "CandidateExperience.JobTitle",
                SourceExcerpt: "Ekip Lideri"),
        };

        var exception = Record.Exception(
            () => EvaluationEvidenceGroundingValidator.EnsureGrounded(evidence));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureGrounded_BilinmeyenAlan_AiEvaluationRequestExceptionFirlatir()
    {
        var evidence = new[]
        {
            new EvaluationEvidenceItem(
                Claim: "Sertifikası var.",
                SourceField: "CandidateProfile.Certifications",
                SourceExcerpt: "AWS Sertifikası"),
        };

        Assert.Throws<AiEvaluationRequestException>(
            () => EvaluationEvidenceGroundingValidator.EnsureGrounded(evidence));
    }

    [Fact]
    public void EnsureGrounded_BosKanitMetni_AiEvaluationRequestExceptionFirlatir()
    {
        var evidence = new[]
        {
            new EvaluationEvidenceItem(
                Claim: "Yönetici deneyimi mevcut.",
                SourceField: "CandidateExperience.JobTitle",
                SourceExcerpt: "   "),
        };

        Assert.Throws<AiEvaluationRequestException>(
            () => EvaluationEvidenceGroundingValidator.EnsureGrounded(evidence));
    }
}
