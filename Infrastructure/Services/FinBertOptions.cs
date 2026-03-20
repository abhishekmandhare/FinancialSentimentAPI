using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Services;

public class FinBertOptions
{
    public const string SectionName = "FinBert";

    [Required]
    [Url]
    public string BaseUrl { get; init; } = "";

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;
}
