using Microsoft.EntityFrameworkCore;

namespace test_project.Data;

/// <summary>
/// Read-only DbContext → PostgreSQL Replica (Lab 4 Read Scaling).
/// </summary>
public class TarotReadDbContext : TarotDbContext
{
    public TarotReadDbContext(DbContextOptions<TarotReadDbContext> options) : base(options)
    {
    }
}
