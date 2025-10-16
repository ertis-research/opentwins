using System.Data;
using Npgsql;

namespace OpenTwinsV2.Things.Infrastructure.Database
{
    public class NpgsqlConnectionFactory : IDbConnectionFactory
    {
        private readonly NpgsqlDataSource _dataSource;

        public NpgsqlConnectionFactory(string connectionString)
        {
            _dataSource = NpgsqlDataSource.Create(connectionString);
        }

        public async Task<NpgsqlConnection> CreateConnection() => await _dataSource.OpenConnectionAsync();
    }
}