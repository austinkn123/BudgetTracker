using BudgetTracker.Domain.Models;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Data;

/// <summary>
/// EF Core context for BudgetTracker (PostgreSQL via Npgsql). Also stores the ASP.NET Core Data Protection
/// key ring, so Plaid access_tokens encrypted on one Lambda instance stay decryptable after a cold start.
/// </summary>
public class BudgetTrackerDbContext(DbContextOptions<BudgetTrackerDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<BudgetPlan> BudgetPlans => Set<BudgetPlan>();
    public DbSet<BudgetPlanEntry> BudgetPlanEntries => Set<BudgetPlanEntry>();
    public DbSet<PlaidItem> PlaidItems => Set<PlaidItem>();
    public DbSet<PlaidAccount> PlaidAccounts => Set<PlaidAccount>();

    /// <inheritdoc />
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <inheritdoc />
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Mirror SQL Server datetime2: wall-clock values, Kind ignored. Without the converter Npgsql
        // throws on DateTime.UtcNow (Kind=Utc) written to a timestamp-without-time-zone column.
        configurationBuilder.Properties<DateTime>()
            .HaveColumnType("timestamp without time zone")
            .HaveConversion<UnspecifiedKindDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BudgetTrackerDbContext).Assembly);
    }
}
