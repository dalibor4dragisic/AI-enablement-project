using System.Text.Json.Serialization;

namespace AIEnablement.Core.Models.DocumentGeneration;

public class ToolIdeationResponse
{
    public List<ToolDescription> Tools { get; set; } = [];
}

public class ToolDescription
{
    public string Name { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string TypicalUserBase { get; set; } = string.Empty;
    public List<string> DocumentTypes { get; set; } = [];
}

public class TableOfContents
{
    public string Title { get; set; } = string.Empty;
    public List<TocSection> Sections { get; set; } = new();
}

public class TocSection
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Level { get; set; }
    public string? ParentId { get; set; }
}

public class TableOfContentsNested
{
    public string Title { get; set; } = string.Empty;
    public List<TocSectionNested> Sections { get; set; } = [];
}

public class TocSectionNested
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Level { get; set; }
    [JsonIgnore]
    public GeneratedSection? GeneratedSection { get; set; }
    [JsonIgnore]
    public bool ShouldHaveIssue { get; set; }
    [JsonIgnore]
    public int NumberOfRequiredIssues { get; set; }
    public List<TocSectionNested> Subsections { get; set; } = [];
}

public class GeneratedSection
{
    public string SectionId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public List<string> InjectedIssues { get; set; } = [];
}

public class GeneratedSectionResponse
{
    public string SectionHtml { get; set; } = string.Empty;
    public List<string> IssuesApplied { get; set; } = [];
}

public class DocumentGenerationContext
{
    public ToolDescription Tool { get; set; } = new();
    public TableOfContentsNested Toc { get; set; } = new();
    public List<GeneratedSection> GeneratedSections { get; set; } = [];
}
