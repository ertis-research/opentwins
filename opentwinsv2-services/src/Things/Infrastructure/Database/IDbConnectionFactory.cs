using System.Data;

namespace OpenTwinsV2.Things.Infrastructure.Database
{
    public interface IDbConnectionFactory
    {
        IDbConnection CreateConnection();
    }
}