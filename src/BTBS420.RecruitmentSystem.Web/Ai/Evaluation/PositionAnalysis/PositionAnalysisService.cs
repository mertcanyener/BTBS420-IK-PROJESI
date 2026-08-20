using System.Text.Json;
using BTBS420.RecruitmentSystem.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation.PositionAnalysis;

public sealed class PositionAnalysisService(
    ApplicationDbContext dbContext,
    IAiEvaluationClient aiEvaluationClient) : IPositionAnalysisService
{
    public const string PromptVersion = "position-analysis-v1";

    private const string SystemPrompt =
        "İK asistanısın. Sadece verilen ilan başlığı ve açıklamasına dayanarak pozisyonu analiz et. " +
        "İlanda açıkça yer almayan zorunlu bir gereksinim uydurma. Aynı beceri veya sertifika, pozisyonun " +
        "rol ailesine ve seviyesine göre farklı önem taşıyabilir; bu farkı 'importanceRationale' alanında " +
        "ve her sorumluluk kategorisinin 'importanceRank' değerinde yansıt. Yanıtı yalnızca verilen JSON " +
        "şemasına birebir uygun, açıklama metni eklemeden döndür.";

    private const string JsonSchema =
        """
        {
          "type": "object",
          "required": [
            "roleFamily", "seniorityLevelName", "seniorityRank",
            "requirementsSummary", "importanceRationale", "responsibilityExpectations"
          ],
          "properties": {
            "roleFamily": { "type": "string" },
            "seniorityLevelName": { "type": "string" },
            "seniorityRank": { "type": "integer" },
            "requirementsSummary": { "type": "string" },
            "importanceRationale": { "type": "string" },
            "responsibilityExpectations": {
              "type": "array",
              "minItems": 1,
              "items": {
                "type": "object",
                "required": ["category", "description", "importanceRank"],
                "properties": {
                  "category": {
                    "type": "string",
                    "enum": ["Technical", "Managerial", "Leadership", "Domain", "Business"]
                  },
                  "description": { "type": "string" },
                  "importanceRank": { "type": "integer" }
                }
              }
            }
          }
        }
        """;

    public async Task<PositionAnalysisResult> AnalyzeAsync(
        int jobPostingId,
        CancellationToken cancellationToken = default)
    {
        var jobPosting = await dbContext.JobPostings
            .AsNoTracking()
            .FirstOrDefaultAsync(posting => posting.Id == jobPostingId, cancellationToken);

        if (jobPosting is null)
        {
            throw new InvalidOperationException($"'{jobPostingId}' numaralı ilan bulunamadı.");
        }

        var request = BuildRequest(jobPosting.Title, jobPosting.Description);
        var response = await aiEvaluationClient.EvaluateAsync(request, cancellationToken);

        return ParseResult(jobPostingId, jobPosting.PositionId, jobPosting.Title, response);
    }

    public static AiEvaluationRequest BuildRequest(string positionTitle, string positionDescription)
    {
        var prompt =
            $"İlan Başlığı: {positionTitle}\n" +
            $"İlan Açıklaması: {positionDescription}\n\n" +
            "Bu ilanı analiz ederek rol ailesini, seniority/leadership seviyesini ve teknik, yönetsel, " +
            "liderlik, domain ile iş sorumluluğu beklentilerini şemaya uygun JSON olarak üret.";

        return new AiEvaluationRequest(
            Prompt: prompt,
            SystemPrompt: SystemPrompt,
            JsonSchema: JsonSchema);
    }

    public static PositionAnalysisResult ParseResult(
        int jobPostingId,
        int positionId,
        string positionTitle,
        AiEvaluationResult aiResult)
    {
        if (aiResult.ParsedJson is null)
        {
            throw new AiEvaluationRequestException("Pozisyon analizi için AI yanıtı ayrıştırılamadı.");
        }

        var root = aiResult.ParsedJson.RootElement;

        var roleFamily = ReadRequiredString(root, "roleFamily");
        var seniorityLevelName = ReadOptionalString(root, "seniorityLevelName");
        var seniorityRank = ReadOptionalInt(root, "seniorityRank");
        var requirementsSummary = ReadRequiredString(root, "requirementsSummary");
        var importanceRationale = ReadRequiredString(root, "importanceRationale");
        var responsibilityExpectations = ReadResponsibilityExpectations(root);

        return new PositionAnalysisResult(
            jobPostingId,
            positionId,
            positionTitle,
            seniorityLevelName,
            seniorityRank,
            requirementsSummary,
            roleFamily,
            responsibilityExpectations,
            importanceRationale);
    }

    private static IReadOnlyList<RoleResponsibilityExpectation> ReadResponsibilityExpectations(JsonElement root)
    {
        if (!root.TryGetProperty("responsibilityExpectations", out var expectationsElement) ||
            expectationsElement.ValueKind != JsonValueKind.Array ||
            expectationsElement.GetArrayLength() == 0)
        {
            throw new AiEvaluationRequestException(
                "Pozisyon analizi en az bir sorumluluk beklentisi içermelidir.");
        }

        var expectations = new List<RoleResponsibilityExpectation>();

        foreach (var item in expectationsElement.EnumerateArray())
        {
            var categoryText = ReadRequiredString(item, "category");

            if (!Enum.TryParse<ResponsibilityCategory>(categoryText, ignoreCase: true, out var category))
            {
                throw new AiEvaluationRequestException(
                    $"Bilinmeyen sorumluluk kategorisi: '{categoryText}'.");
            }

            var description = ReadRequiredString(item, "description");
            var importanceRank = ReadRequiredInt(item, "importanceRank");

            expectations.Add(new RoleResponsibilityExpectation(category, description, importanceRank));
        }

        return expectations;
    }

    private static string ReadRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new AiEvaluationRequestException($"AI yanıtında '{propertyName}' alanı eksik veya boş.");
        }

        return value.GetString()!;
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static int ReadRequiredInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var number))
        {
            throw new AiEvaluationRequestException($"AI yanıtında '{propertyName}' alanı eksik veya sayısal değil.");
        }

        return number;
    }

    private static int? ReadOptionalInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var number))
        {
            return null;
        }

        return number;
    }
}
