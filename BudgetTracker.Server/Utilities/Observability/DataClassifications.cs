using Microsoft.Extensions.Compliance.Classification;

namespace BudgetTracker.Server.Utilities.Observability;

/// <summary>
/// The classes of data that must never reach the logs in the clear (project-docs/observability.md §3). Tag a
/// <c>[LoggerMessage]</c> parameter with one of the attributes below and its value is erased before it is written.
/// </summary>
public static class DataClassifications
{
    /// <summary>The taxonomy name shared by every classification here.</summary>
    public const string Taxonomy = "BudgetTracker";

    /// <summary>Amounts, balances, merchant or payee names, and account names.</summary>
    public static DataClassification Financial { get; } = new(Taxonomy, nameof(Financial));

    /// <summary>Plaid access tokens, Webull secrets, and <c>Authorization</c> header values.</summary>
    public static DataClassification Credential { get; } = new(Taxonomy, nameof(Credential));

    /// <summary>Email addresses, the Cognito <c>sub</c>, and anything else that identifies a person.</summary>
    public static DataClassification Personal { get; } = new(Taxonomy, nameof(Personal));
}

/// <summary>Marks a logged value as <see cref="DataClassifications.Financial"/>; it is erased from the log.</summary>
public sealed class FinancialDataAttribute() : DataClassificationAttribute(DataClassifications.Financial);

/// <summary>Marks a logged value as <see cref="DataClassifications.Credential"/>; it is erased from the log.</summary>
public sealed class CredentialDataAttribute() : DataClassificationAttribute(DataClassifications.Credential);

/// <summary>Marks a logged value as <see cref="DataClassifications.Personal"/>; it is erased from the log.</summary>
public sealed class PersonalDataAttribute() : DataClassificationAttribute(DataClassifications.Personal);
