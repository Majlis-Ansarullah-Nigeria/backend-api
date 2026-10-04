using System.Diagnostics.CodeAnalysis;

namespace HRS.Api.IntegrationTests;

[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Collection is xunit's documented naming convention for a [CollectionDefinition] fixture.")]
[CollectionDefinition(Name)]
public sealed class ApiTestCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "Api";
}
