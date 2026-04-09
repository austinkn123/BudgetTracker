using BudgetTracker.Domain.Common;
using BudgetTracker.Domain.Interfaces.Accessors;
using BudgetTracker.Domain.Interfaces.Engines;
using BudgetTracker.Domain.Interfaces.Managers;
using BudgetTracker.Domain.Models;

namespace BudgetTracker.Server.Managers;

public class TransactionManager(ITransactionEngine engine, ITransactionAccessor accessor) : ITransactionManager
{
    public async Task<Result<Transaction>> GetByIdAsync(int id, int userId)
    {
        var transaction = await accessor.GetByIdAsync(id, userId);
        return transaction is not null
            ? Result<Transaction>.Success(transaction)
            : Result<Transaction>.Failure("Transaction not found");
    }

    public async Task<Result<IEnumerable<Transaction>>> GetByUserIdAsync(int userId)
    {
        var transactions = await accessor.GetByUserIdAsync(userId);
        return Result<IEnumerable<Transaction>>.Success(transactions);
    }

    public async Task<Result<int>> CreateAsync(Transaction transaction, int userId)
    {
        var error = engine.ValidateTransaction(transaction);
        if (error is not null)
            return Result<int>.Failure(error);

        var ownsAccount = await accessor.AccountBelongsToUserAsync(transaction.AccountId, userId);
        if (!ownsAccount)
            return Result<int>.Failure("Account not found for current user");

        if (transaction.TransferAccountId is not null)
        {
            var ownsTransferAccount = await accessor.AccountBelongsToUserAsync(transaction.TransferAccountId.Value, userId);
            if (!ownsTransferAccount)
                return Result<int>.Failure("Transfer account not found for current user");
        }

        var id = await accessor.CreateAsync(transaction);
        return Result<int>.Success(id);
    }

    public async Task<Result<bool>> UpdateAsync(Transaction transaction, int userId)
    {
        var error = engine.ValidateTransaction(transaction);
        if (error is not null)
            return Result<bool>.Failure(error);

        var ownsAccount = await accessor.AccountBelongsToUserAsync(transaction.AccountId, userId);
        if (!ownsAccount)
            return Result<bool>.Failure("Account not found for current user");

        if (transaction.TransferAccountId is not null)
        {
            var ownsTransferAccount = await accessor.AccountBelongsToUserAsync(transaction.TransferAccountId.Value, userId);
            if (!ownsTransferAccount)
                return Result<bool>.Failure("Transfer account not found for current user");
        }

        var updated = await accessor.UpdateAsync(transaction, userId);
        return updated
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Transaction not found");
    }

    public async Task<Result<bool>> DeleteAsync(int id, int userId)
    {
        var deleted = await accessor.DeleteAsync(id, userId);
        return deleted
            ? Result<bool>.Success(true)
            : Result<bool>.Failure("Transaction not found");
    }
}
