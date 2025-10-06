using System.Data;
using System.Text.Json;
using Dapr.Client;
using Npgsql;
using NpgsqlTypes;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Things.Logging;
using OpenTwinsV2.Things.Infrastructure.Database;
using Dapr.Actors;
using Dapr;
using System.Text.Json.Nodes;

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

        public async Task<string> AddLinkAsync(string linkJson)
        {
            if (ThingDescription is null)
                await LoadAsync();

            if (string.IsNullOrWhiteSpace(linkJson))
                throw new ArgumentException("Link JSON cannot be null or empty.", nameof(linkJson));

            Link? newLink;
            try
            {
                newLink = JsonSerializer.Deserialize<Link>(linkJson);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid link JSON format.", ex);
            }

            if (newLink is null)
                throw new ArgumentException("The link is invalid.");

            // Initialize list if null
            ThingDescription!.Links ??= [];

            // Avoid duplicates: replace if same href already exists
            var existing = ThingDescription.Links.FirstOrDefault(l => l.Href == newLink.Href);
            string eventName = "added";
            if (existing != null)
            {
                ThingDescription.Links.Remove(existing);
                eventName = "updated";
            }

            ThingDescription.Links.Add(newLink);
            await SaveAsync(ThingDescription);
            /*
            var metadata = new Dictionary<string, string>() {
                { "cloudevent.source", new Uri(_thingId).ToString() },
                { "cloudevent.type", "link." + eventName }
            };*/
            //await _daprClient.PublishEventAsync("kafka-pubsub", "opentwinsv2.links.changes", newLink, metadata);
            var httpClient = DaprClient.CreateInvokeHttpClient();
            var response = await httpClient.PostAsJsonAsync("http://twins-service/internal/events/links/" + Uri.EscapeDataString(_thingId) + "/link." + eventName, 
                                    JsonSerializer.Serialize(newLink));
            var result = await response.Content.ReadAsStringAsync();

            ActorLogger.Info(_thingId, $"Published '{eventName}' link event for href '{newLink.Href}'.");

            return ThingDescription.ToString()!;
        }

        public async Task RemoveLinkAsync(string href)
        {
            if (ThingDescription is null)
                await LoadAsync();

            if (!string.IsNullOrWhiteSpace(href) && Uri.TryCreate(href, UriKind.Absolute, out Uri? uri))
            {
                if (ThingDescription!.Links is null || ThingDescription.Links.Count == 0)
                    throw new KeyNotFoundException($"No links found for ThingId {_thingId}.");

                var link = ThingDescription.Links.FirstOrDefault(l => l.Href == uri) ?? throw new KeyNotFoundException($"Link with href '{href}' not found in ThingId {_thingId}.");

                ThingDescription.Links.Remove(link);
                await SaveAsync(ThingDescription);
                var cloudEvent = new CloudEvent<Link>(link)
                {
                    Source = new Uri(_thingId),
                    Type = "link.removed"
                };
                await _daprClient.PublishEventAsync("kafka-pubsub", "opentwinsv2.links.changes", cloudEvent);
                ActorLogger.Info(_thingId, $"Published 'removed' link event for href '{href}'.");
            }
            else
            {
                throw new ArgumentException("Href cannot be null or empty.", nameof(href));
            }
        }
    }
}
