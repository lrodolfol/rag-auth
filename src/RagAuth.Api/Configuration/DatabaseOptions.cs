using System.ComponentModel.DataAnnotations;

namespace RagAuth.Api.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "database";

    [Required(ErrorMessage = "A configuração 'database:host' é obrigatória.")]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "A configuração 'database:port' deve estar entre 1 e 65535.")]
    public int Port { get; set; } = 5432;

    [Required(ErrorMessage = "A configuração 'database:name' é obrigatória.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "A configuração 'database:username' é obrigatória.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "A variável de ambiente DATABASE_PASSWORD é obrigatória.")]
    public string Password { get; set; } = string.Empty;
}
