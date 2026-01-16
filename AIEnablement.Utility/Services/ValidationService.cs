using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AIEnablement.Utility.Services;

public interface IValidationService
{
    bool ValidateHtml(string html, out List<string> errors);
    bool ValidateJson(string json, out List<string> errors);
}

public class ValidationService : IValidationService
{
    private readonly ILogger<ValidationService> _logger;

    public ValidationService(ILogger<ValidationService> logger)
    {
        _logger = logger;
    }

    public bool ValidateHtml(string html, out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(html))
        {
            errors.Add("HTML content is empty or whitespace");
            return false;
        }

        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            if (doc.ParseErrors != null && doc.ParseErrors.Any())
            {
                foreach (var error in doc.ParseErrors)
                {
                    errors.Add($"Line {error.Line}, Position {error.LinePosition}: {error.Reason}");
                }
            }

            if (errors.Any())
            {
                _logger.LogWarning("HTML validation found {Count} issues", errors.Count);
                return false;
            }

            _logger.LogDebug("HTML validation passed");
            return true;
        }
        catch (Exception ex)
        {
            errors.Add($"HTML parsing exception: {ex.Message}");
            _logger.LogError(ex, "HTML validation failed with exception");
            return false;
        }
    }

    public bool ValidateJson(string json, out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(json))
        {
            errors.Add("JSON content is empty or whitespace");
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            
            _logger.LogDebug("JSON validation passed");
            return true;
        }
        catch (JsonException ex)
        {
            errors.Add($"JSON parsing error: {ex.Message}");
            _logger.LogError(ex, "JSON validation failed");
            return false;
        }
    }
}
