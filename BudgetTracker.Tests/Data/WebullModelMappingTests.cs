using BudgetTracker.Domain.Data;
using BudgetTracker.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace BudgetTracker.Tests.Data;

/// <summary>
/// Pins richie's Webull schema decisions in the model the migration is generated from: unique indexes, check constraints,
/// delete behaviors and column types. Built against the Npgsql provider (no connection is opened), like
/// <see cref="DateTimeColumnMappingTests"/>. The constraints themselves were exercised on Postgres when the migration was applied.
/// </summary>
public class WebullModelMappingTests
{
    private static readonly IModel Model = BuildModel();

    private static IModel BuildModel()
    {
        var options = new DbContextOptionsBuilder<BudgetTrackerDbContext>()
            .UseNpgsql("Host=localhost;Database=model-only")
            .Options;
        using var context = new BudgetTrackerDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entity<T>() => Model.FindEntityType(typeof(T))!;

    private static IIndex IndexNamed<T>(string name) =>
        Assert.Single(Entity<T>().GetIndexes(), i => i.GetDatabaseName() == name);

    [Fact]
    public void UniqueIndexes_AreUniqueOnTheRightColumns()
    {
        var perUser = IndexNamed<WebullConnection>("UQ_WebullConnections_UserId");
        var perAccount = IndexNamed<WebullAccount>("UQ_WebullAccounts_WebullConnectionId_AccountId");

        Assert.True(perUser.IsUnique);
        Assert.Equal([nameof(WebullConnection.UserId)], perUser.Properties.Select(p => p.Name));
        Assert.True(perAccount.IsUnique);
        Assert.Equal(
            [nameof(WebullAccount.WebullConnectionId), nameof(WebullAccount.AccountId)],
            perAccount.Properties.Select(p => p.Name));
    }

    [Fact]
    public void CheckConstraints_HaveTheAgreedNamesAndSql()
    {
        var constraints = Entity<WebullConnection>().GetCheckConstraints().ToDictionary(c => c.ModelName, c => c.Sql);

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["CK_WebullConnections_TokenStatus"] = "\"TokenStatus\" IN ('Pending', 'Normal', 'Expired', 'Invalid')",
                ["CK_WebullConnections_LastError"] = "\"LastError\" IN ('Unreachable', 'KeyRejected', 'TokenRejected')",
                ["CK_WebullConnections_Token"] =
                    "(\"AccessTokenEncrypted\" IS NULL AND \"TokenStatus\" IS NULL AND \"TokenCreatedAt\" IS NULL) OR " +
                    "(\"AccessTokenEncrypted\" IS NOT NULL AND \"TokenStatus\" IS NOT NULL AND \"TokenCreatedAt\" IS NOT NULL)"
            },
            constraints);
    }

    [Fact]
    public void WebullAccounts_CascadeWithTheirConnection()
    {
        var fk = Assert.Single(Entity<WebullAccount>().GetForeignKeys());

        Assert.Equal(typeof(WebullConnection), fk.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
    }

    [Fact]
    public void WebullConnection_ForeignKeyToUsers_IsNoAction()
    {
        var fk = Assert.Single(Entity<WebullConnection>().GetForeignKeys());

        Assert.Equal(typeof(User), fk.PrincipalEntityType.ClrType);
        Assert.Equal([nameof(WebullConnection.UserId)], fk.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior);
    }

    [Theory]
    [InlineData(typeof(WebullAccount), nameof(WebullAccount.TotalNetLiquidationValue), "numeric(18,4)")]
    [InlineData(typeof(WebullAccount), nameof(WebullAccount.TotalCashBalance), "numeric(18,4)")]
    [InlineData(typeof(WebullConnection), nameof(WebullConnection.AccessTokenEncrypted), "character varying(1000)")]
    [InlineData(typeof(WebullAccount), nameof(WebullAccount.TotalMarketValue), "numeric(18,4)")]
    [InlineData(typeof(WebullAccount), nameof(WebullAccount.TotalDayProfitLoss), "numeric(18,4)")]
    [InlineData(typeof(WebullAccount), nameof(WebullAccount.TotalUnrealizedProfitLoss), "numeric(18,4)")]
    [InlineData(typeof(WebullAccountPosition), nameof(WebullAccountPosition.Quantity), "numeric(28,10)")]
    [InlineData(typeof(WebullAccountPosition), nameof(WebullAccountPosition.LastPrice), "numeric(28,10)")]
    [InlineData(typeof(WebullAccountPosition), nameof(WebullAccountPosition.CostPrice), "numeric(28,10)")]
    [InlineData(typeof(WebullAccountPosition), nameof(WebullAccountPosition.UnrealizedProfitLoss), "numeric(18,4)")]
    [InlineData(typeof(WebullAccountPosition), nameof(WebullAccountPosition.Symbol), "character varying(50)")]
    [InlineData(typeof(WebullDailyValue), nameof(WebullDailyValue.EasternDate), "date")]
    [InlineData(typeof(WebullDailyValue), nameof(WebullDailyValue.TotalValue), "numeric(18,4)")]
    [InlineData(typeof(WebullDailyValue), nameof(WebullDailyValue.Cash), "numeric(18,4)")]
    [InlineData(typeof(WebullDailyValue), nameof(WebullDailyValue.MarketValue), "numeric(18,4)")]
    [InlineData(typeof(WebullDailyValue), nameof(WebullDailyValue.DayProfitLoss), "numeric(18,4)")]
    [InlineData(typeof(WebullDailyValue), nameof(WebullDailyValue.UnrealizedProfitLoss), "numeric(18,4)")]
    [InlineData(typeof(WebullDailyValue), nameof(WebullDailyValue.AccountId), "character varying(100)")]
    [InlineData(typeof(WebullDailyValue), nameof(WebullDailyValue.FetchedAt), "timestamp without time zone")]
    public void Columns_HaveTheAgreedTypes(Type entity, string property, string columnType)
    {
        Assert.Equal(columnType, Model.FindEntityType(entity)!.FindProperty(property)!.GetColumnType());
    }

    // ── Holdings (Phase 2) ──────────────────────────────────────────────────

    [Fact]
    public void HoldingsTables_HaveTheAgreedNames()
    {
        Assert.Equal("WebullPositions", Entity<WebullAccountPosition>().GetTableName());
        Assert.Equal("WebullDailyValues", Entity<WebullDailyValue>().GetTableName());
    }

    [Fact]
    public void WebullAccountPosition_CascadesWithItsAccount_AndHasNoUniqueIndex()
    {
        var fk = Assert.Single(Entity<WebullAccountPosition>().GetForeignKeys());

        Assert.Equal(typeof(WebullAccount), fk.PrincipalEntityType.ClrType);
        Assert.Equal([nameof(WebullAccountPosition.WebullAccountId)], fk.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
        Assert.DoesNotContain(Entity<WebullAccountPosition>().GetIndexes(), i => i.IsUnique);
    }

    [Fact]
    public void WebullDailyValue_KeyIsConnectionThenDateThenAccount()
    {
        Assert.Equal(
            [nameof(WebullDailyValue.WebullConnectionId), nameof(WebullDailyValue.EasternDate), nameof(WebullDailyValue.AccountId)],
            Entity<WebullDailyValue>().FindPrimaryKey()!.Properties.Select(p => p.Name));
    }

    [Fact]
    public void WebullDailyValue_OnlyForeignKeyIsItsConnection_WithCascade()
    {
        // No FK to WebullAccounts: replacing the snapshot must never delete history.
        var fk = Assert.Single(Entity<WebullDailyValue>().GetForeignKeys());

        Assert.Equal(typeof(WebullConnection), fk.PrincipalEntityType.ClrType);
        Assert.Equal([nameof(WebullDailyValue.WebullConnectionId)], fk.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
    }

    [Fact]
    public void WebullDailyValue_HasNoSecondaryIndex()
    {
        Assert.Empty(Entity<WebullDailyValue>().GetIndexes());
    }

    // ── Trade history (Phase 3) ─────────────────────────────────────────────

    [Fact]
    public void TradeHistoryTables_HaveTheAgreedNames()
    {
        Assert.Equal("WebullOrders", Entity<WebullAccountOrder>().GetTableName());
        Assert.Equal("WebullOrderSyncs", Entity<WebullAccountOrderSync>().GetTableName());
    }

    [Fact]
    public void TradeHistoryKeys_AreConnectionThenAccountThenOrder()
    {
        Assert.Equal(
            [nameof(WebullAccountOrder.WebullConnectionId), nameof(WebullAccountOrder.AccountId), nameof(WebullAccountOrder.OrderId)],
            Entity<WebullAccountOrder>().FindPrimaryKey()!.Properties.Select(p => p.Name));
        Assert.Equal(
            [nameof(WebullAccountOrderSync.WebullConnectionId), nameof(WebullAccountOrderSync.AccountId)],
            Entity<WebullAccountOrderSync>().FindPrimaryKey()!.Properties.Select(p => p.Name));
    }

    [Theory]
    [InlineData(typeof(WebullAccountOrder))]
    [InlineData(typeof(WebullAccountOrderSync))]
    public void TradeHistoryTables_OnlyForeignKeyIsTheirConnection_WithCascade_AndNoIndexes(Type entity)
    {
        // No FK to WebullAccounts: replacing the balance snapshot must never delete history.
        var type = Model.FindEntityType(entity)!;
        var fk = Assert.Single(type.GetForeignKeys());

        Assert.Equal(typeof(WebullConnection), fk.PrincipalEntityType.ClrType);
        Assert.Equal(["WebullConnectionId"], fk.Properties.Select(p => p.Name));
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
        Assert.Empty(type.GetIndexes());
        Assert.Null(type.FindPrimaryKey()!.Properties.FirstOrDefault(p => p.ValueGenerated != ValueGenerated.Never));
    }

    [Fact]
    public void WebullOrders_HaveNoCheckConstraints_SyncsHaveTheAgreedThree()
    {
        Assert.Empty(Entity<WebullAccountOrder>().GetCheckConstraints());
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["CK_WebullOrderSyncs_LastError"] = "\"LastError\" IN ('Unreachable', 'KeyRejected', 'TokenRejected')",
                ["CK_WebullOrderSyncs_Covered"] =
                    "(\"CoveredFrom\" IS NULL AND \"CoveredTo\" IS NULL) OR (\"CoveredFrom\" IS NOT NULL AND \"CoveredTo\" IS NOT NULL)",
                ["CK_WebullOrderSyncs_Window"] =
                    "(\"WindowStart\" IS NULL AND \"WindowEnd\" IS NULL AND \"PaginationKey\" IS NULL) OR " +
                    "(\"WindowStart\" IS NOT NULL AND \"WindowEnd\" IS NOT NULL)"
            },
            Entity<WebullAccountOrderSync>().GetCheckConstraints().ToDictionary(c => c.ModelName, c => c.Sql));
    }

    [Theory]
    [InlineData(typeof(WebullAccountOrder), nameof(WebullAccountOrder.AccountId), "character varying(100)", false)]
    [InlineData(typeof(WebullAccountOrder), nameof(WebullAccountOrder.OrderId), "character varying(100)", false)]
    [InlineData(typeof(WebullAccountOrder), nameof(WebullAccountOrder.Symbol), "character varying(50)", false)]
    [InlineData(typeof(WebullAccountOrder), nameof(WebullAccountOrder.InstrumentType), "character varying(50)", true)]
    [InlineData(typeof(WebullAccountOrder), nameof(WebullAccountOrder.Side), "character varying(50)", false)]
    [InlineData(typeof(WebullAccountOrder), nameof(WebullAccountOrder.Status), "character varying(50)", false)]
    [InlineData(typeof(WebullAccountOrder), nameof(WebullAccountOrder.FilledQuantity), "numeric(28,10)", true)]
    [InlineData(typeof(WebullAccountOrder), nameof(WebullAccountOrder.FilledPrice), "numeric(28,10)", true)]
    [InlineData(typeof(WebullAccountOrder), nameof(WebullAccountOrder.PlacedAt), "timestamp without time zone", false)]
    [InlineData(typeof(WebullAccountOrder), nameof(WebullAccountOrder.FilledAt), "timestamp without time zone", true)]
    [InlineData(typeof(WebullAccountOrderSync), nameof(WebullAccountOrderSync.AccountId), "character varying(100)", false)]
    [InlineData(typeof(WebullAccountOrderSync), nameof(WebullAccountOrderSync.CoveredFrom), "timestamp without time zone", true)]
    [InlineData(typeof(WebullAccountOrderSync), nameof(WebullAccountOrderSync.CoveredTo), "timestamp without time zone", true)]
    [InlineData(typeof(WebullAccountOrderSync), nameof(WebullAccountOrderSync.WindowStart), "timestamp without time zone", true)]
    [InlineData(typeof(WebullAccountOrderSync), nameof(WebullAccountOrderSync.WindowEnd), "timestamp without time zone", true)]
    [InlineData(typeof(WebullAccountOrderSync), nameof(WebullAccountOrderSync.PaginationKey), "character varying(2000)", true)]
    [InlineData(typeof(WebullAccountOrderSync), nameof(WebullAccountOrderSync.LastError), "character varying(20)", true)]
    public void TradeHistoryColumns_HaveTheAgreedTypes(Type entity, string property, string columnType, bool nullable)
    {
        var column = Model.FindEntityType(entity)!.FindProperty(property)!;

        Assert.Equal(columnType, column.GetColumnType());
        Assert.Equal(nullable, column.IsNullable);
        Assert.Null(column.GetDefaultValueSql());
    }
}
