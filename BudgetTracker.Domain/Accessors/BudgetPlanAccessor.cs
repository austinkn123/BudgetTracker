using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Domain.Accessors;

public class BudgetPlanAccessor(BudgetTrackerDbContext context) : IBudgetPlanAccessor
{
    public async Task<BudgetPlan?> GetByIdAsync(int id, int userId)
    {
        return await context.BudgetPlans
            .AsNoTracking()
            .Where(bp => bp.Id == id && bp.UserId == userId)
            .Select(bp => new BudgetPlan
            {
                Id = bp.Id,
                UserId = bp.UserId,
                Name = bp.Name,
                PlanMonth = bp.PlanMonth,
                NetIncomeMonthly = bp.NetIncomeMonthly,
                IsActive = bp.IsActive,
                CreatedAt = bp.CreatedAt,
                UpdatedAt = bp.UpdatedAt,
                Entries = bp.Entries
                    .OrderBy(l => l.SortOrder)
                    .ThenBy(l => l.Id)
                    .Select(l => new BudgetPlanEntry
                    {
                        Id = l.Id,
                        BudgetPlanId = l.BudgetPlanId,
                        CategoryId = l.CategoryId,
                        LineType = l.LineType,
                        Bucket = l.Bucket,
                        Cadence = l.Cadence,
                        Amount = l.Amount,
                        MonthlyEquivalent = l.MonthlyEquivalent,
                        IsStressFactor = l.IsStressFactor,
                        Notes = l.Notes,
                        SortOrder = l.SortOrder,
                        CreatedAt = l.CreatedAt,
                        UpdatedAt = l.UpdatedAt
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();
    }

    public async Task<IEnumerable<BudgetPlan>> GetByUserIdAsync(int userId)
    {
        return await context.BudgetPlans
            .AsNoTracking()
            .Where(bp => bp.UserId == userId)
            .OrderByDescending(bp => bp.PlanMonth)
            .ThenByDescending(bp => bp.IsActive)
            .ThenBy(bp => bp.Name)
            .Select(bp => new BudgetPlan
            {
                Id = bp.Id,
                UserId = bp.UserId,
                Name = bp.Name,
                PlanMonth = bp.PlanMonth,
                NetIncomeMonthly = bp.NetIncomeMonthly,
                IsActive = bp.IsActive,
                CreatedAt = bp.CreatedAt,
                UpdatedAt = bp.UpdatedAt,
                Entries = bp.Entries
                    .OrderBy(l => l.SortOrder)
                    .ThenBy(l => l.Id)
                    .Select(l => new BudgetPlanEntry
                    {
                        Id = l.Id,
                        BudgetPlanId = l.BudgetPlanId,
                        CategoryId = l.CategoryId,
                        LineType = l.LineType,
                        Bucket = l.Bucket,
                        Cadence = l.Cadence,
                        Amount = l.Amount,
                        MonthlyEquivalent = l.MonthlyEquivalent,
                        IsStressFactor = l.IsStressFactor,
                        Notes = l.Notes,
                        SortOrder = l.SortOrder,
                        CreatedAt = l.CreatedAt,
                        UpdatedAt = l.UpdatedAt
                    })
                    .ToList()
            })
            .ToListAsync();
    }

    public async Task<bool> CategoriesBelongToUserAsync(IEnumerable<int> categoryIds, int userId)
    {
        var ids = categoryIds.Distinct().ToArray();
        if (ids.Length == 0)
        {
            return true;
        }

        var count = await context.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .CountAsync(c => ids.Contains(c.Id));

        return count == ids.Length;
    }

    public async Task<int> CreateAsync(BudgetPlan budgetPlan)
    {
        context.BudgetPlans.Add(budgetPlan);
        await context.SaveChangesAsync();
        return budgetPlan.Id;
    }

    public async Task<bool> UpdateAsync(BudgetPlan budgetPlan, int userId)
    {
        var existing = await context.BudgetPlans
            .Include(bp => bp.Entries)
            .Where(bp => bp.Id == budgetPlan.Id && bp.UserId == userId)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            return false;
        }

        existing.Name = budgetPlan.Name;
        existing.PlanMonth = budgetPlan.PlanMonth;
        existing.NetIncomeMonthly = budgetPlan.NetIncomeMonthly;
        existing.IsActive = budgetPlan.IsActive;
        existing.UpdatedAt = DateTime.UtcNow;

        var incomingEntryIds = budgetPlan.Entries
            .Where(l => l.Id > 0)
            .Select(l => l.Id)
            .ToHashSet();

        var entriesToRemove = existing.Entries
            .Where(l => !incomingEntryIds.Contains(l.Id))
            .ToList();

        foreach (var entryToRemove in entriesToRemove)
        {
            context.BudgetPlanEntries.Remove(entryToRemove);
        }

        foreach (var incomingEntry in budgetPlan.Entries)
        {
            if (incomingEntry.Id <= 0)
            {
                existing.Entries.Add(new BudgetPlanEntry
                {
                    CategoryId = incomingEntry.CategoryId,
                    LineType = incomingEntry.LineType,
                    Bucket = incomingEntry.Bucket,
                    Cadence = incomingEntry.Cadence,
                    Amount = incomingEntry.Amount,
                    MonthlyEquivalent = incomingEntry.MonthlyEquivalent,
                    IsStressFactor = incomingEntry.IsStressFactor,
                    Notes = incomingEntry.Notes,
                    SortOrder = incomingEntry.SortOrder,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                continue;
            }

            var existingEntry = existing.Entries.FirstOrDefault(l => l.Id == incomingEntry.Id);
            if (existingEntry is null)
            {
                return false;
            }

            existingEntry.CategoryId = incomingEntry.CategoryId;
            existingEntry.LineType = incomingEntry.LineType;
            existingEntry.Bucket = incomingEntry.Bucket;
            existingEntry.Cadence = incomingEntry.Cadence;
            existingEntry.Amount = incomingEntry.Amount;
            existingEntry.MonthlyEquivalent = incomingEntry.MonthlyEquivalent;
            existingEntry.IsStressFactor = incomingEntry.IsStressFactor;
            existingEntry.Notes = incomingEntry.Notes;
            existingEntry.SortOrder = incomingEntry.SortOrder;
            existingEntry.UpdatedAt = DateTime.UtcNow;
        }

        return await context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteAsync(int id, int userId)
    {
        var budgetPlan = await context.BudgetPlans
            .Where(bp => bp.Id == id && bp.UserId == userId)
            .FirstOrDefaultAsync();

        if (budgetPlan is null)
        {
            return false;
        }

        context.BudgetPlans.Remove(budgetPlan);
        return await context.SaveChangesAsync() > 0;
    }
}