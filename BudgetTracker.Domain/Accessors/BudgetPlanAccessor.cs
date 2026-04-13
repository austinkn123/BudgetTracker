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
                Lines = bp.Lines
                    .OrderBy(l => l.SortOrder)
                    .ThenBy(l => l.Id)
                    .Select(l => new BudgetPlanLine
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
                Lines = bp.Lines
                    .OrderBy(l => l.SortOrder)
                    .ThenBy(l => l.Id)
                    .Select(l => new BudgetPlanLine
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
            .Include(bp => bp.Lines)
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

        var incomingLineIds = budgetPlan.Lines
            .Where(l => l.Id > 0)
            .Select(l => l.Id)
            .ToHashSet();

        var linesToRemove = existing.Lines
            .Where(l => !incomingLineIds.Contains(l.Id))
            .ToList();

        foreach (var lineToRemove in linesToRemove)
        {
            context.BudgetPlanLines.Remove(lineToRemove);
        }

        foreach (var incomingLine in budgetPlan.Lines)
        {
            if (incomingLine.Id <= 0)
            {
                existing.Lines.Add(new BudgetPlanLine
                {
                    CategoryId = incomingLine.CategoryId,
                    LineType = incomingLine.LineType,
                    Bucket = incomingLine.Bucket,
                    Cadence = incomingLine.Cadence,
                    Amount = incomingLine.Amount,
                    MonthlyEquivalent = incomingLine.MonthlyEquivalent,
                    IsStressFactor = incomingLine.IsStressFactor,
                    Notes = incomingLine.Notes,
                    SortOrder = incomingLine.SortOrder,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
                continue;
            }

            var existingLine = existing.Lines.FirstOrDefault(l => l.Id == incomingLine.Id);
            if (existingLine is null)
            {
                return false;
            }

            existingLine.CategoryId = incomingLine.CategoryId;
            existingLine.LineType = incomingLine.LineType;
            existingLine.Bucket = incomingLine.Bucket;
            existingLine.Cadence = incomingLine.Cadence;
            existingLine.Amount = incomingLine.Amount;
            existingLine.MonthlyEquivalent = incomingLine.MonthlyEquivalent;
            existingLine.IsStressFactor = incomingLine.IsStressFactor;
            existingLine.Notes = incomingLine.Notes;
            existingLine.SortOrder = incomingLine.SortOrder;
            existingLine.UpdatedAt = DateTime.UtcNow;
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