namespace Pos.Infrastructure.Seeding;

public sealed class SeedOptions
{
    public const string SectionName = "Seed";
    /// <summary>Path to the seed JSON file. Relative paths resolve against the content root.</summary>
    public string FilePath { get; set; } = "seed.json";
}
