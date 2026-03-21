using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BudgetTracker.Domain.Data;

public class DapperContext(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("BudgetTrackerConnection")
        ?? throw new InvalidOperationException("Connection string 'BudgetTrackerConnection' not found.");

    public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
}
