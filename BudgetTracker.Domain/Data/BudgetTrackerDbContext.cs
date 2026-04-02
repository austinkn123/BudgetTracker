using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Data;

public class BudgetTrackerDbContext(DbContextOptions<BudgetTrackerDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BudgetTrackerDbContext).Assembly);
    }
}
