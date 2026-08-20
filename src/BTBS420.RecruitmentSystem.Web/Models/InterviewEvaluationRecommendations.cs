using System.Collections.Frozen;

namespace BTBS420.RecruitmentSystem.Web.Models;

public static class InterviewEvaluationRecommendations
{
    public const string Positive = "positive";
    public const string Negative = "negative";

    private static readonly FrozenSet<string> DefinedValues =
        new[] { Positive, Negative }.ToFrozenSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> All => DefinedValues;

    public static bool IsDefined(string value)
    {
        return DefinedValues.Contains(value);
    }

    public static string GetDisplayLabel(string value)
    {
        return value switch
        {
            Positive => "Olumlu",
            Negative => "Olumsuz",
            _ => value
        };
    }
}
