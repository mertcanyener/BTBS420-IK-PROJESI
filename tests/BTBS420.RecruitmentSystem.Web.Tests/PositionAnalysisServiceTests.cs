using System.Text.Json;
using BTBS420.RecruitmentSystem.Web.Ai;
using BTBS420.RecruitmentSystem.Web.Ai.Evaluation;
using BTBS420.RecruitmentSystem.Web.Ai.Evaluation.PositionAnalysis;

namespace BTBS420.RecruitmentSystem.Web.Tests;

public sealed class PositionAnalysisServiceTests
{
    private const string JuniorAiResponse =
        """
        {
          "roleFamily": "Yazılım Mühendisliği",
          "seniorityLevelName": "Junior",
          "seniorityRank": 1,
          "requirementsSummary": "Junior seviyede temel programlama bilgisi ve öğrenmeye açıklık aranıyor.",
          "importanceRationale": "Junior seviyede en çok temel teknik yeterlilik önemlidir; yönetsel/liderlik beklentisi yoktur.",
          "responsibilityExpectations": [
            { "category": "Technical", "description": "Temel programlama dili bilgisi", "importanceRank": 1 },
            { "category": "Domain", "description": "Sektöre yönelik temel farkındalık", "importanceRank": 2 },
            { "category": "Business", "description": "Takım içinde görev alma", "importanceRank": 3 },
            { "category": "Managerial", "description": "Yönetsel beklenti yok", "importanceRank": 4 },
            { "category": "Leadership", "description": "Liderlik beklentisi yok", "importanceRank": 5 }
          ]
        }
        """;

    private const string SeniorAiResponse =
        """
        {
          "roleFamily": "Yazılım Mühendisliği",
          "seniorityLevelName": "Senior",
          "seniorityRank": 3,
          "requirementsSummary": "Senior seviyede derin teknik uzmanlık ve bağımsız problem çözme aranıyor.",
          "importanceRationale": "Senior seviyede en çok derin teknik uzmanlık ve bağımsız çözüm üretme önemlidir.",
          "responsibilityExpectations": [
            { "category": "Technical", "description": "Karmaşık sistem tasarımı", "importanceRank": 1 },
            { "category": "Domain", "description": "Sektöre özgü derin bilgi", "importanceRank": 2 },
            { "category": "Leadership", "description": "Junior mühendislere teknik rehberlik", "importanceRank": 3 },
            { "category": "Business", "description": "İş gereksinimlerini teknik çözüme dökme", "importanceRank": 4 },
            { "category": "Managerial", "description": "Yönetsel beklenti sınırlı", "importanceRank": 5 }
          ]
        }
        """;

    private const string LeadAiResponse =
        """
        {
          "roleFamily": "Yazılım Mühendisliği",
          "seniorityLevelName": "Lead",
          "seniorityRank": 4,
          "requirementsSummary": "Lead seviyede teknik derinlik ile ekip yönlendirmenin dengeli birleşimi aranıyor.",
          "importanceRationale": "Lead seviyede teknik derinlik ile liderlik eşit ölçüde önemlidir.",
          "responsibilityExpectations": [
            { "category": "Technical", "description": "Mimari kararlara teknik katkı", "importanceRank": 1 },
            { "category": "Leadership", "description": "Ekip koordinasyonu ve teknik yönlendirme", "importanceRank": 1 },
            { "category": "Domain", "description": "Sektörel derinlik", "importanceRank": 3 },
            { "category": "Business", "description": "Yol haritası önceliklendirme", "importanceRank": 4 },
            { "category": "Managerial", "description": "Sınırlı performans yönetimi", "importanceRank": 5 }
          ]
        }
        """;

    private const string ManagerAiResponse =
        """
        {
          "roleFamily": "Yazılım Mühendisliği",
          "seniorityLevelName": "Manager",
          "seniorityRank": 5,
          "requirementsSummary": "Manager seviyede ekip yönetimi ve stratejik karar verme aranıyor.",
          "importanceRationale": "Manager seviyede en çok liderlik ve yönetsel yetkinlik önemlidir; derin teknik uygulama beklenmez.",
          "responsibilityExpectations": [
            { "category": "Leadership", "description": "Ekip yönlendirme ve mentorluk", "importanceRank": 1 },
            { "category": "Managerial", "description": "Kaynak ve performans yönetimi", "importanceRank": 2 },
            { "category": "Business", "description": "Paydaş yönetimi ve bütçe sorumluluğu", "importanceRank": 3 },
            { "category": "Domain", "description": "Sektörel stratejik farkındalık", "importanceRank": 4 },
            { "category": "Technical", "description": "Genel teknik farkındalık", "importanceRank": 5 }
          ]
        }
        """;

    [Fact]
    public void ParseResult_JuniorSeviyesinde_TeknikBeklentiEnOnemliOlur()
    {
        var result = ParseResult(JuniorAiResponse);

        Assert.Equal("Junior", result.SeniorityLevelName);
        Assert.Equal(1, ExpectationFor(result, ResponsibilityCategory.Technical).ImportanceRank);
        Assert.Equal(5, ExpectationFor(result, ResponsibilityCategory.Leadership).ImportanceRank);
    }

    [Fact]
    public void ParseResult_SeniorSeviyesinde_TeknikDerinlikEnOnemliOlur()
    {
        var result = ParseResult(SeniorAiResponse);

        Assert.Equal("Senior", result.SeniorityLevelName);
        Assert.Equal(1, ExpectationFor(result, ResponsibilityCategory.Technical).ImportanceRank);
        Assert.Equal(5, ExpectationFor(result, ResponsibilityCategory.Managerial).ImportanceRank);
    }

    [Fact]
    public void ParseResult_LeadSeviyesinde_TeknikVeLiderlikEsitAgirliktaOlur()
    {
        var result = ParseResult(LeadAiResponse);

        Assert.Equal("Lead", result.SeniorityLevelName);
        Assert.Equal(1, ExpectationFor(result, ResponsibilityCategory.Technical).ImportanceRank);
        Assert.Equal(1, ExpectationFor(result, ResponsibilityCategory.Leadership).ImportanceRank);
    }

    [Fact]
    public void ParseResult_ManagerSeviyesinde_LiderlikBeklentisiEnOnemliOlur()
    {
        var result = ParseResult(ManagerAiResponse);

        Assert.Equal("Manager", result.SeniorityLevelName);
        Assert.Equal(1, ExpectationFor(result, ResponsibilityCategory.Leadership).ImportanceRank);
        Assert.Equal(5, ExpectationFor(result, ResponsibilityCategory.Technical).ImportanceRank);
    }

    [Fact]
    public void ParseResult_AyniKategorininOnemiSeviyeyeGoreFarklilasir()
    {
        var junior = ParseResult(JuniorAiResponse);
        var manager = ParseResult(ManagerAiResponse);

        var juniorTechnicalRank = ExpectationFor(junior, ResponsibilityCategory.Technical).ImportanceRank;
        var managerTechnicalRank = ExpectationFor(manager, ResponsibilityCategory.Technical).ImportanceRank;
        var juniorLeadershipRank = ExpectationFor(junior, ResponsibilityCategory.Leadership).ImportanceRank;
        var managerLeadershipRank = ExpectationFor(manager, ResponsibilityCategory.Leadership).ImportanceRank;

        Assert.NotEqual(juniorTechnicalRank, managerTechnicalRank);
        Assert.NotEqual(juniorLeadershipRank, managerLeadershipRank);
        Assert.NotEqual(junior.ImportanceRationale, manager.ImportanceRationale);
    }

    [Fact]
    public void ParseResult_AiYanitiAyristirilamadiysa_AiEvaluationRequestExceptionFirlatir()
    {
        var aiResult = new AiEvaluationResult(
            RawText: string.Empty,
            ParsedJson: null,
            Metadata: CreateMetadata());

        Assert.Throws<AiEvaluationRequestException>(
            () => PositionAnalysisService.ParseResult(1, 1, "Yazılım Mühendisi", aiResult));
    }

    [Fact]
    public void BuildRequest_IlanBasligiVeAciklamasiniPromptaEkler()
    {
        var request = PositionAnalysisService.BuildRequest(
            "Kıdemli Yazılım Mühendisi",
            "Dağıtık sistemler konusunda deneyimli mühendis aranıyor.");

        Assert.Contains("Kıdemli Yazılım Mühendisi", request.Prompt);
        Assert.Contains("Dağıtık sistemler konusunda deneyimli mühendis aranıyor.", request.Prompt);
        Assert.False(string.IsNullOrWhiteSpace(request.SystemPrompt));
        Assert.False(string.IsNullOrWhiteSpace(request.JsonSchema));
    }

    private static PositionAnalysisResult ParseResult(string rawJson)
    {
        var aiResult = new AiEvaluationResult(
            RawText: rawJson,
            ParsedJson: JsonDocument.Parse(rawJson),
            Metadata: CreateMetadata());

        return PositionAnalysisService.ParseResult(
            jobPostingId: 1,
            positionId: 1,
            positionTitle: "Yazılım Mühendisi",
            aiResult);
    }

    private static RoleResponsibilityExpectation ExpectationFor(
        PositionAnalysisResult result,
        ResponsibilityCategory category)
    {
        return result.ResponsibilityExpectations.Single(item => item.Category == category);
    }

    private static AiResponseMetadata CreateMetadata()
    {
        return new AiResponseMetadata(
            Provider: "Test",
            Model: "test-model",
            ModelVersion: "1.0",
            InputTokens: 10,
            OutputTokens: 10,
            LatencyMs: 5);
    }
}
