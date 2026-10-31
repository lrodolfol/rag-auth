namespace RagAuth.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<RagAuthApiFactory>
{
    public const string Name = "Integração com PostgreSQL";
}
