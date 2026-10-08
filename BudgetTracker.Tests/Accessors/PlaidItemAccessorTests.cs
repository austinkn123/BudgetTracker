using BudgetTracker.Domain.Accessors;
using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Models;
using BudgetTracker.Domain.Plaid;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BudgetTracker.Tests.Accessors;

/// <summary>
/// Token-read behavior of <see cref="PlaidItemAccessor"/>. A token written under one key ring and read under another
/// reproduces a lost Data Protection key ring (for example, a Lambda cold start before keys were persisted).
/// </summary>
public class PlaidItemAccessorTests : IDisposable
{
    private const int UserId = 5;
    private readonly BudgetTrackerDbContext _context;
    private readonly EphemeralDataProtectionProvider _currentKeyRing = new();

    public PlaidItemAccessorTests()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new BudgetTrackerDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    private PlaidItemAccessor BuildSut() => new(_context, _currentKeyRing);

    private async Task<PlaidItem> SeedItemAsync(IDataProtectionProvider keyRing, bool isActive = true)
    {
        var protector = keyRing.CreateProtector(PlaidItemAccessor.DataProtectionPurpose);
        var item = new PlaidItem
        {
            UserId = UserId,
            PlaidItemId = "plaid-item-1",
            InstitutionId = "ins_3",
            InstitutionName = "Chase",
            AccessTokenEncrypted = protector.Protect("access-token-xyz"),
            IsActive = isActive
        };
        _context.PlaidItems.Add(item);
        await _context.SaveChangesAsync();
        return item;
    }

    [Fact]
    public async Task Should_ReturnFoundWithToken_When_ActiveItemTokenDecrypts()
    {
        var item = await SeedItemAsync(_currentKeyRing);

        var lookup = await BuildSut().GetActiveAccessTokenAsync(UserId, item.Id);

        Assert.Equal(AccessTokenLookup.Found("access-token-xyz"), lookup);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ItemIsInactive()
    {
        var item = await SeedItemAsync(_currentKeyRing, isActive: false);

        var lookup = await BuildSut().GetActiveAccessTokenAsync(UserId, item.Id);

        Assert.Equal(AccessTokenStatus.NotFound, lookup.Status);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_ItemBelongsToAnotherUser()
    {
        var item = await SeedItemAsync(_currentKeyRing);

        var lookup = await BuildSut().GetActiveAccessTokenAsync(userId: 999, item.Id);

        Assert.Equal(AccessTokenStatus.NotFound, lookup.Status);
    }

    [Fact]
    public async Task Should_ReturnUndecryptableWithoutThrowing_When_TokenWasEncryptedWithALostKeyRing()
    {
        var item = await SeedItemAsync(new EphemeralDataProtectionProvider());

        var lookup = await BuildSut().GetActiveAccessTokenAsync(UserId, item.Id);

        Assert.Equal(AccessTokenLookup.Undecryptable, lookup);
    }

    [Fact]
    public async Task Should_ReturnFoundWithToken_When_LookingUpByPlaidItemIdAndTokenDecrypts()
    {
        await SeedItemAsync(_currentKeyRing);

        var lookup = await BuildSut().GetAccessTokenByPlaidItemIdAsync("plaid-item-1");

        Assert.Equal(AccessTokenLookup.Found("access-token-xyz"), lookup);
    }

    [Fact]
    public async Task Should_ReturnNotFound_When_PlaidItemIdIsUnknown()
    {
        var lookup = await BuildSut().GetAccessTokenByPlaidItemIdAsync("unknown-item");

        Assert.Equal(AccessTokenStatus.NotFound, lookup.Status);
    }

    [Fact]
    public async Task Should_ReturnUndecryptableWithoutThrowing_When_LookingUpByPlaidItemIdWithALostKeyRing()
    {
        await SeedItemAsync(new EphemeralDataProtectionProvider());

        var lookup = await BuildSut().GetAccessTokenByPlaidItemIdAsync("plaid-item-1");

        Assert.Equal(AccessTokenLookup.Undecryptable, lookup);
    }

    [Fact]
    public async Task Reactivate_InactiveItemOwnedByUser_SetsActiveAndReturnsTrue()
    {
        var item = await SeedItemAsync(_currentKeyRing, isActive: false);

        var reactivated = await BuildSut().ReactivateAsync(UserId, item.Id);

        Assert.True(reactivated);
        Assert.True((await _context.PlaidItems.AsNoTracking().SingleAsync(p => p.Id == item.Id)).IsActive);
    }

    [Fact]
    public async Task Reactivate_ItemOwnedByAnotherUser_ReturnsFalseAndLeavesItInactive()
    {
        var item = await SeedItemAsync(_currentKeyRing, isActive: false);

        var reactivated = await BuildSut().ReactivateAsync(userId: 999, item.Id);

        Assert.False(reactivated);
        Assert.False((await _context.PlaidItems.AsNoTracking().SingleAsync(p => p.Id == item.Id)).IsActive);
    }

    [Fact]
    public async Task Reactivate_AlreadyActiveOrUnknownItem_ReturnsFalse()
    {
        var item = await SeedItemAsync(_currentKeyRing);

        Assert.False(await BuildSut().ReactivateAsync(UserId, item.Id));
        Assert.False(await BuildSut().ReactivateAsync(UserId, plaidItemId: 404));
    }
}
