using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Server.Managers;

/// <inheritdoc />
public class CategoryRuleManager(
    ICategoryRuleEngine engine,
    ICategoryRuleAccessor ruleAccessor,
    ICategoryAccessor categoryAccessor,
    ITransactionAccessor transactionAccessor,
    IPlaidEngine plaidEngine,
    IDataVersionAccessor dataVersion) : ICategoryRuleManager
{
    private const string NotFound = "Rule not found";
    private const string CategoryNotFound = "Category not found";

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<CategoryRuleView>>> GetAllAsync(int userId)
    {
        var rules = await ruleAccessor.GetByUserIdAsync(userId);
        IReadOnlyList<PayeeCategoryCount> counts = rules.Count == 0 ? [] : await transactionAccessor.GetPayeeCategoryCountsAsync(userId);

        IReadOnlyList<CategoryRuleView> views = rules
            .Select(r => new CategoryRuleView(r.Id, r.Pattern, r.MatchType, r.CategoryId, r.Category.Name, engine.CountMatched(r, rules, counts)))
            .ToList();
        return Result<IReadOnlyList<CategoryRuleView>>.Success(views);
    }

    /// <inheritdoc />
    public async Task<Result<int>> PreviewAsync(CategoryRule candidate)
    {
        if (await ValidateAsync(candidate) is { } error)
            return Result<int>.Failure(error);

        // Not saved yet, so it is the newest rule: it wins ties against older rules of the same length.
        candidate.CreatedAt = DateTime.UtcNow;
        var rules = (await ruleAccessor.GetByUserIdAsync(candidate.UserId)).Append(candidate).ToList();
        var counts = await transactionAccessor.GetPayeeCategoryCountsAsync(candidate.UserId);

        return Result<int>.Success(engine.CountUncategorizedWon(candidate, rules, counts));
    }

    /// <inheritdoc />
    public async Task<Result<CategoryRuleCreated>> CreateAsync(CategoryRule rule, bool applyToExisting)
    {
        if (await ValidateAsync(rule) is { } error)
            return Result<CategoryRuleCreated>.Failure(error);

        int id;
        try
        {
            id = await ruleAccessor.CreateAsync(rule);
        }
        catch (UniqueNameViolationException)
        {
            return Result<CategoryRuleCreated>.Failure(engine.DuplicateRuleError(rule.Pattern));
        }
        catch (MissingReferenceException)
        {
            return Result<CategoryRuleCreated>.Failure(CategoryNotFound);
        }

        var applied = 0;
        try
        {
            if (applyToExisting)
                applied = await FileUncategorizedRowsAsync(id, rule.UserId);
        }
        finally
        {
            // The rule is saved even if filing throws, so clients must refetch either way.
            await dataVersion.BumpAsync(rule.UserId);
        }

        return Result<CategoryRuleCreated>.Success(new CategoryRuleCreated(id, applied));
    }

    /// <inheritdoc />
    public async Task<Result<bool>> UpdateAsync(CategoryRule rule)
    {
        if (await ValidateAsync(rule) is { } error)
            return Result<bool>.Failure(error);

        bool updated;
        try
        {
            updated = await ruleAccessor.UpdateAsync(rule);
        }
        catch (UniqueNameViolationException)
        {
            return Result<bool>.Failure(engine.DuplicateRuleError(rule.Pattern));
        }
        catch (MissingReferenceException)
        {
            return Result<bool>.Failure(CategoryNotFound);
        }

        if (!updated)
            return Result<bool>.Failure(NotFound);

        await dataVersion.BumpAsync(rule.UserId);
        return Result<bool>.Success(true);
    }

    /// <inheritdoc />
    public async Task<Result<bool>> DeleteAsync(int id, int userId)
    {
        if (!await ruleAccessor.DeleteAsync(id, userId))
            return Result<bool>.Failure(NotFound);

        await dataVersion.BumpAsync(userId);
        return Result<bool>.Success(true);
    }

    /// <summary>Normalizes the pattern in place, then validates the rule and that its category is the user's own.</summary>
    private async Task<string?> ValidateAsync(CategoryRule rule)
    {
        rule.Pattern = engine.NormalizePattern(rule.Pattern);
        if (engine.ValidateRule(rule) is { } error)
            return error;

        // The category id arrives straight from the request body.
        return await categoryAccessor.GetByIdForUserAsync(rule.CategoryId, rule.UserId) is null ? CategoryNotFound : null;
    }

    /// <summary>Files the uncategorized rows whose payee the saved rule wins, re-typing each as a hand-picked category would.</summary>
    private async Task<int> FileUncategorizedRowsAsync(int ruleId, int userId)
    {
        var rules = await ruleAccessor.GetByUserIdAsync(userId);
        var rule = rules.First(r => r.Id == ruleId);
        var counts = await transactionAccessor.GetPayeeCategoryCountsAsync(userId);

        var payees = engine.PayeesWonBy(rule, rules, counts.Where(c => c.CategoryId is null).Select(c => c.Payee));
        var rows = await transactionAccessor.GetUncategorizedByPayeesAsync(userId, payees);
        if (rows.Count == 0)
            return 0;

        var types = rows.ToDictionary(
            r => r.Id,
            r => plaidEngine.ClassifyTransactionType(r.PlaidCategoryPrimary, rule.Category.CategoryType, r.TransactionType));
        return await transactionAccessor.SetCategoryAsync(types, rule.CategoryId, userId);
    }
}
