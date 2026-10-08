namespace BudgetTracker.Domain.Data;

/// <summary>
/// Builds LIKE/ILIKE patterns from user input so that wildcard characters in the input are matched literally.
/// </summary>
public static class LikePattern
{
    /// <summary>The escape character that must be passed alongside patterns built here.</summary>
    public const string EscapeCharacter = @"\";

    /// <summary>
    /// Builds a "contains" pattern for <paramref name="term"/>, escaping <c>\</c>, <c>%</c> and <c>_</c>.
    /// </summary>
    /// <param name="term">The raw user-supplied search term.</param>
    /// <returns>The term escaped and wrapped in <c>%</c> wildcards.</returns>
    public static string Contains(string term)
    {
        ArgumentNullException.ThrowIfNull(term);

        // The escape character goes first so the backslashes added for % and _ are not escaped again.
        var escaped = term
            .Replace(EscapeCharacter, EscapeCharacter + EscapeCharacter)
            .Replace("%", EscapeCharacter + "%")
            .Replace("_", EscapeCharacter + "_");

        return $"%{escaped}%";
    }
}
