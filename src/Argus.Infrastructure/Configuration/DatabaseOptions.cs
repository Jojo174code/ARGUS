namespace Argus.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Provider { get; init; } = "Postgres";

    public string DatabaseName { get; init; } = "argus";

    public string ConnectionString { get; init; } = string.Empty;
}