using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pos.Domain.Catalog;
using Pos.Infrastructure.Persistence;

namespace Pos.Infrastructure.Seeding;

public sealed class DatabaseSeeder(PosDbContext db, IOptions<SeedOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Seeds products from the JSON file if the catalog is empty. Idempotent across restarts.</summary>
    public async Task SeedAsync(string contentRootPath, CancellationToken ct = default)
    {
        if (await db.Products.AnyAsync(ct))
            return;

        var path = Path.IsPathRooted(options.Value.FilePath)
            ? options.Value.FilePath
            : Path.Combine(contentRootPath, options.Value.FilePath);

        await using var stream = File.OpenRead(path);
        var seed = await JsonSerializer.DeserializeAsync<SeedFile>(stream, JsonOptions, ct)
            ?? throw new InvalidOperationException("Seed file could not be parsed.");

        foreach (var p in seed.Products)
        {
            var category = Enum.Parse<ProductCategory>(p.Category, ignoreCase: true);
            var product = new Product(p.Name, category, p.PriceCents, p.StockQuantity, p.ImageKey);
            foreach (var (culture, name) in p.Translations)
                product.AddTranslation(culture, name);
            db.Products.Add(product);
        }

        await db.SaveChangesAsync(ct);
    }
}
