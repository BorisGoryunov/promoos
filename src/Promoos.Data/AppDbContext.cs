using Microsoft.EntityFrameworkCore;
using Promoos.Data.Configurations;

namespace Promoos.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfiguration(new TaskItemConfig());
    }
}
