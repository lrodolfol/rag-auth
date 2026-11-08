namespace RagAuth.Api.ErrorHandling;

/// <summary>
/// Marca rotas cujo caminho carrega valores secretos (ex.: selector/validator do link de confirmação).
/// Nos erros dessas rotas, <c>instance</c> usa o modelo da rota em vez do caminho real (FR-021, SC-008).
/// </summary>
public sealed class SensitiveRouteValuesMetadata;
