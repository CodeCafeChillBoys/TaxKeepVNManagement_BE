using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TaxKeepVN.Infrastructure.Contexts
{
    public class TaxKeepDbContextFactory : IDesignTimeDbContextFactory<TaxKeepDbContext>
    {
        public TaxKeepDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<TaxKeepDbContext>();
            var connectionString = "Host=aws-0-ap-northeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.povlmvqhanglccvmfadc;Password=TaxKeepVNBE;SSL Mode=Require;Trust Server Certificate=true;Timeout=60;Command Timeout=120;";
            optionsBuilder.UseNpgsql(connectionString);

            return new TaxKeepDbContext(optionsBuilder.Options);
        }
    }
}
