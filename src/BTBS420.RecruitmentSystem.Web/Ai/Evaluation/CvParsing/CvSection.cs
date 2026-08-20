namespace BTBS420.RecruitmentSystem.Web.Ai.Evaluation.CvParsing;

public sealed record CvSection(CvSectionKind Kind, IReadOnlyList<string> Entries);
