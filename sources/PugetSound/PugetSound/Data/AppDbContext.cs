using Microsoft.EntityFrameworkCore;
using PugetSound.Data.Models;

namespace PugetSound.Data
{
    public class AppDbContext : DbContext
    {
        public DbSet<UserScore> UserScores { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> dbContextOptions) : base(dbContextOptions)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // ⚠ don't remove

            builder.Entity<UserScore>()
                .HasIndex(x => x.Id)
                .IsUnique();
        }
    }
}
