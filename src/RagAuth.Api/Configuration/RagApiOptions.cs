using System.ComponentModel.DataAnnotations;

namespace RagAuth.Api.Configuration;

public sealed class RagApiOptions
{
    public const string SectionName = "ragApi";

    [Required(ErrorMessage = "A configuração 'ragApi:baseUrl' é obrigatória.")]
    [Url(ErrorMessage = "A configuração 'ragApi:baseUrl' deve ser uma URL absoluta.")]
    public string BaseUrl { get; set; } = string.Empty;

    [Required(ErrorMessage = "A configuração 'ragApi:businessPath' é obrigatória.")]
    public string BusinessPath { get; set; } = "/business";

    [Range(1, 120, ErrorMessage = "A configuração 'ragApi:timeoutSeconds' deve estar entre 1 e 120.")]
    public int TimeoutSeconds { get; set; } = 10;

    [Required(ErrorMessage = "A variável de ambiente RAG_API_KEY é obrigatória.")]
    public string ApiKey { get; set; } = string.Empty;
}
