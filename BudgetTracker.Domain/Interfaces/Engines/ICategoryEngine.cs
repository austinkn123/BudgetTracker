using BudgetTracker.Domain.Models;

namespace BudgetTracker.Domain.Interfaces.Engines;

public interface ICategoryEngine
{
    string? ValidateCategory(Category category);
}
