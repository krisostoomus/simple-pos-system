using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pos.Infrastructure.Persistence;

public sealed class PosDbContextFactory : IDesignTimeDbContextFactory<PosDbContext>
{
    public PosDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("POS_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=pos;Username=pos;Password=pos";
        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseNpgsql(connection)
            .Options;
        return new PosDbContext(options);
    }
}
