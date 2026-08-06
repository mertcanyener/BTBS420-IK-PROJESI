using System.Text;

namespace BTBS420.RecruitmentSystem.Web.ActivityLogging;

public static class CsvExportHelper
{
    private static readonly char[] FormulaTriggerPrefixes = ['=', '+', '-', '@', '\t', '\r'];

    public static byte[] BuildCsv(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(rows);

        var builder = new StringBuilder();
        AppendRow(builder, headers);

        foreach (var row in rows)
        {
            AppendRow(builder, row.Select(value => value ?? string.Empty));
        }

        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(builder.ToString());
    }

    private static void AppendRow(StringBuilder builder, IEnumerable<string> values)
    {
        builder.AppendJoin(',', values.Select(EscapeField));
        builder.Append("\r\n");
    }

    private static string EscapeField(string value)
    {
        var sanitized = SanitizeFormulaInjection(value);

        return sanitized.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{sanitized.Replace("\"", "\"\"")}\""
            : sanitized;
    }

    private static string SanitizeFormulaInjection(string value)
    {
        if (value.Length == 0)
        {
            return value;
        }

        return FormulaTriggerPrefixes.Contains(value[0]) ? $"'{value}" : value;
    }
}
