using LoyalStamp.Data;
using Microsoft.EntityFrameworkCore;

namespace LoyalStamp.Shared
{
    public class UserIdGenerator
    {
        private readonly IDbContextFactory<ApplicationDbContext> _dbContextFactory;

        public UserIdGenerator(IDbContextFactory<ApplicationDbContext> dbContextFactory)
        {
            this._dbContextFactory = dbContextFactory;
        }

        // generate a unique customer ID by fetching the next value from the CustomerIdSeq sequence in the database and formatting it as a 12-character string starting with '1'. Example: if the next value is 1, the generated ID will be "100000000001".
        public async Task<string> GenerateCustomerIdAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var nextVal = await GetNextSequenceValueAsync(db, "CustomerIdSeq");
            return $"1{nextVal:D11}";
        }

        // generate a unique merchant ID by fetching the next value from the MerchantIdSeq sequence in the database and formatting it as a 12-character string starting with '2'. Example: if the next value is 1, the generated ID will be "200000000001".
        public async Task<string> GenerateMerchantIdAsync()
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync();
            var nextVal = await GetNextSequenceValueAsync(db, "MerchantIdSeq");
            return $"2{nextVal:D11}";
        }

        private static async Task<long> GetNextSequenceValueAsync(ApplicationDbContext db, string sequenceName)
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = $"SELECT NEXT VALUE FOR {sequenceName}";
            await db.Database.OpenConnectionAsync();
            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }
    }
}
