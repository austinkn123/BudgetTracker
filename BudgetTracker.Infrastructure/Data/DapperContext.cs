using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using System.Data;
namespace BudgetTracker.Infrastructure.Data
{
    public class DapperContext(IConfiguration configuration)
    {
        private readonly string _connectionString = configuration.GetConnectionString("BudgetTrackerConnection");

        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
    }
}
