using System.Data;
using System.Text.Json;
using Dapr.Client;
using Npgsql;
using NpgsqlTypes;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Things.Logging;
using OpenTwinsV2.Things.Infrastructure.Database;
using Dapr.Actors;

namespace OpenTwinsV2.Things.Actors.Services
{
    internal class ThingDescriptionManager
    {
        private const string ThingDescriptionKey = "TD_";
        private readonly DaprClient _daprClient;
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly string _thingId;
        private const string StateStoreName = "actorstatestore";

        public ThingDescription? ThingDescription { get; private set; }

        public ThingDescriptionManager(DaprClient daprClient, IDbConnectionFactory connectionFactory, string thingId)
        {
            _daprClient = daprClient;
            _connectionFactory = connectionFactory;
            _thingId = thingId;
        }

        public async Task LoadAsync()
        {
            var bulkStateItems = await _daprClient.GetBulkStateAsync<string>(StateStoreName, [ThingDescriptionKey + _thingId], parallelism: 1);
            if (bulkStateItems.Count > 0 && !string.IsNullOrEmpty(bulkStateItems[0].Value))
            {
                ThingDescription = JsonSerializer.Deserialize<ThingDescription>(bulkStateItems[0].Value);
                ActorLogger.Info(_thingId, "Thing Description loaded from statestore");
            }
            else
            {
                ActorLogger.Info(_thingId, "No ThingDescription found in statestore.");
                ThingDescription = await LoadFromPostgreSqlAsync();
            }
        }

        public async Task SaveAsync(ThingDescription td)
        {
            ThingDescription = td;
            await _daprClient.SaveStateAsync("actorstatestore", ThingDescriptionKey + _thingId, td.ToString());
            await SaveToPostgreSqlAsync(td);
        }

        private async Task<ThingDescription?> LoadFromPostgreSqlAsync()
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection is NpgsqlConnection npgsql) await npgsql.OpenAsync();
            else connection.Open();

            var cmd = new NpgsqlCommand(
                "SELECT td FROM thing_descriptions WHERE thingId = @ThingId;",
                (NpgsqlConnection)connection);
            cmd.Parameters.Add(new NpgsqlParameter("@ThingId", DbType.String) { Value = _thingId });

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var json = reader.GetString(0);
                var td = JsonSerializer.Deserialize<ThingDescription>(json);
                ActorLogger.Info(_thingId, "Thing Description loaded from PostgreSQL.");
                return td;
            }

            ActorLogger.Info(_thingId, $"No ThingDescription found in PostgreSQL for {_thingId}.");
            return null;
        }

        private async Task SaveToPostgreSqlAsync(ThingDescription td)
        {
            using var connection = _connectionFactory.CreateConnection();
            if (connection is NpgsqlConnection npgsql) await npgsql.OpenAsync();
            else connection.Open();

            var cmd = new NpgsqlCommand(
                    @"INSERT INTO thing_descriptions (thingId, td)
                    VALUES (@ThingId, @NewTd)
                    ON CONFLICT (thingId) DO UPDATE SET td = @NewTd;",
                (NpgsqlConnection)connection);

            cmd.Parameters.Add(new NpgsqlParameter("@ThingId", DbType.String) { Value = td.Id });
            cmd.Parameters.Add(new NpgsqlParameter("@NewTd", NpgsqlDbType.Jsonb) { Value = td.ToString() });

            int affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0) ActorLogger.Warn(_thingId, $"No row affected for ThingId {td.Id}");

            ActorLogger.Info(_thingId, "Thing Description saved in PostgreSQL.");
        }
    }
}
