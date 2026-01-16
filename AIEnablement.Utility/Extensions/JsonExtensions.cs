namespace AIEnablement.Utility.Extensions;

public static class JsonExtensions
{
    public static string? CleanJson(this string? json)
    {
        var cleanJson = json?.Replace("```json", "")
            .Replace("```", "")
            .Trim();

        return cleanJson;
    }
}
