using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Herohold.Storage;

/// <summary>Used only by <c>dotnet ef migrations</c>.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<HeroholdDbContext>
{
    public HeroholdDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<HeroholdDbContext>().UseSqlite("Data Source=design-time.db").Options);
}
