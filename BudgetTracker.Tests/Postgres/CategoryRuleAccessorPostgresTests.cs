using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Tests.Postgres;

/// <summary>
/// CategoryRuleAccessor against real Postgres (BUD-24): the case-insensitive unique expression index, the foreign key
/// that keeps a category in use, and per-user scoping.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Postgres")]
public sealed class CategoryRuleAccessorPostgresTests(PostgresFixture fixture)
{
    private async Task<int> CreateRuleAsync(int userId, int categoryId, string pattern, string matchType = CategoryRuleMatchType.Contains)
    {
        await using var context = fixture.CreateContext();
        return await new CategoryRuleAccessor(context).CreateAsync(
            new CategoryRule { UserId = userId, CategoryId = categoryId, Pattern = pattern, MatchType = matchType });
    }

    [PostgresFact]
    public async Task Should_ThrowUniqueViolation_When_PatternDiffersOnlyByCase()
    {
        var user = await fixture.UserAsync();
        var coffee = await fixture.CategoryAsync(user.Id, "Coffee");
        await CreateRuleAsync(user.Id, coffee.Id, "Starbucks");

        var violation = await Assert.ThrowsAsync<UniqueNameViolationException>(() => CreateRuleAsync(user.Id, coffee.Id, "STARBUCKS"));

        Assert.Equal("UQ_CategoryRules_User_Pattern", violation.ConstraintName);
    }

    [PostgresFact]
    public async Task Should_AllowTheSamePattern_When_MatchTypeOrUserDiffers()
    {
        var user = await fixture.UserAsync();
        var other = await fixture.UserAsync();
        var coffee = await fixture.CategoryAsync(user.Id, "Coffee");
        var otherCoffee = await fixture.CategoryAsync(other.Id, "Coffee");
        await CreateRuleAsync(user.Id, coffee.Id, "starbucks");

        Assert.True(await CreateRuleAsync(user.Id, coffee.Id, "starbucks", CategoryRuleMatchType.Exact) > 0);
        Assert.True(await CreateRuleAsync(other.Id, otherCoffee.Id, "starbucks") > 0);
    }

    [PostgresFact]
    public async Task Should_ThrowUniqueViolation_When_UpdateCollidesWithAnotherRule()
    {
        var user = await fixture.UserAsync();
        var coffee = await fixture.CategoryAsync(user.Id, "Coffee");
        await CreateRuleAsync(user.Id, coffee.Id, "starbucks");
        var second = await CreateRuleAsync(user.Id, coffee.Id, "peets");

        await using var context = fixture.CreateContext();
        var violation = await Assert.ThrowsAsync<UniqueNameViolationException>(() => new CategoryRuleAccessor(context).UpdateAsync(
            new CategoryRule { Id = second, UserId = user.Id, CategoryId = coffee.Id, Pattern = "Starbucks", MatchType = CategoryRuleMatchType.Contains }));

        Assert.Equal("UQ_CategoryRules_User_Pattern", violation.ConstraintName);
    }

    [PostgresFact]
    public async Task Should_KeepACategoryInUse_When_ARuleFilesIntoIt()
    {
        var user = await fixture.UserAsync();
        var coffee = await fixture.CategoryAsync(user.Id, "Coffee");
        await CreateRuleAsync(user.Id, coffee.Id, "starbucks");

        await using var context = fixture.CreateContext();
        var inUse = await Assert.ThrowsAsync<EntityInUseException>(() => new CategoryAccessor(context).DeleteAsync(coffee.Id, user.Id));

        Assert.Equal("FK_CategoryRules_Categories_CategoryId", inUse.ConstraintName);
    }

    [PostgresFact]
    public async Task Should_ScopeEveryReadAndWriteToTheUser()
    {
        var user = await fixture.UserAsync();
        var other = await fixture.UserAsync();
        var coffee = await fixture.CategoryAsync(user.Id, "Coffee");
        var rule = await CreateRuleAsync(user.Id, coffee.Id, "starbucks");

        await using var context = fixture.CreateContext();
        var accessor = new CategoryRuleAccessor(context);

        Assert.Empty(await accessor.GetByUserIdAsync(other.Id));
        Assert.Null(await accessor.GetByIdForUserAsync(rule, other.Id));
        Assert.Empty(await accessor.GetPatternsForCategoryAsync(coffee.Id, other.Id));
        Assert.False(await accessor.UpdateAsync(new CategoryRule
        {
            Id = rule, UserId = other.Id, CategoryId = coffee.Id, Pattern = "hijacked", MatchType = CategoryRuleMatchType.Exact,
        }));
        Assert.False(await accessor.DeleteAsync(rule, other.Id));

        var mine = Assert.Single(await accessor.GetByUserIdAsync(user.Id));
        Assert.Equal(("starbucks", "Coffee"), (mine.Pattern, mine.Category.Name));
        Assert.Equal(["starbucks"], await accessor.GetPatternsForCategoryAsync(coffee.Id, user.Id));
    }

    [PostgresFact]
    public async Task Should_SaveTheNewPatternMatchTypeAndCategory_When_Updated()
    {
        var user = await fixture.UserAsync();
        var coffee = await fixture.CategoryAsync(user.Id, "Coffee");
        var dining = await fixture.CategoryAsync(user.Id, "Dining");
        var rule = await CreateRuleAsync(user.Id, coffee.Id, "starbucks");

        await using (var context = fixture.CreateContext())
        {
            Assert.True(await new CategoryRuleAccessor(context).UpdateAsync(new CategoryRule
            {
                Id = rule, UserId = user.Id, CategoryId = dining.Id, Pattern = "Starbucks Reserve", MatchType = CategoryRuleMatchType.Exact,
            }));
        }

        await using var check = fixture.CreateContext();
        var saved = await new CategoryRuleAccessor(check).GetByIdForUserAsync(rule, user.Id);
        Assert.Equal(("Starbucks Reserve", CategoryRuleMatchType.Exact, dining.Id), (saved!.Pattern, saved.MatchType, saved.CategoryId));
    }

    [PostgresFact]
    public async Task Should_RemoveOnlyTheRule_When_Deleted()
    {
        var user = await fixture.UserAsync();
        var coffee = await fixture.CategoryAsync(user.Id, "Coffee");
        var rule = await CreateRuleAsync(user.Id, coffee.Id, "starbucks");

        await using (var context = fixture.CreateContext())
            Assert.True(await new CategoryRuleAccessor(context).DeleteAsync(rule, user.Id));

        await using var check = fixture.CreateContext();
        Assert.False(await check.CategoryRules.AnyAsync(r => r.Id == rule));
        Assert.True(await check.Categories.AnyAsync(c => c.Id == coffee.Id));
    }
}
