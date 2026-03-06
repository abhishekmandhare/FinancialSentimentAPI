using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Services;

public class OllamaOptions
{
    public const string SectionName = "Ollama";

    [Required]
    [Url]
    public string BaseUrl { get; init; } = "";

    [Required]
    public string Model { get; init; } = "llama3";

    [Range(1, 4096)]
    public int MaxTokens { get; init; } = 512;
}
