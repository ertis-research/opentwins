using System.Data;
using Npgsql;

namespace OpenTwinsV2.Things.Infrastructure.Database
{
    public interface IDbConnectionFactory
    {
        Task<NpgsqlConnection> CreateConnection();
    }
}