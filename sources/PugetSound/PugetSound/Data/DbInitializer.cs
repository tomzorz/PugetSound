using Microsoft.EntityFrameworkCore;

namespace PugetSound.Data
{
    public static class DbInitializer
    {
        public static void Initialize(AppDbContext context)
        {
            context.Database.EnsureCreated();

            context.Database.Migrate();
        }
    }
}