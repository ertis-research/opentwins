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

        public async Task SaveAsync(ThingDescription td, bool asyncPersist = true)
        {
            ThingDescription = td;
            await _daprClient.SaveStateAsync("actorstatestore", ThingDescriptionKey + _thingId, td.ToString());

            if (asyncPersist)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var metadata = new Dictionary<string, string>() {
                            { "cloudevent.source", new Uri(_thingId).ToString() },
                            { "cloudevent.type", "thing.description.changes:" + _thingId}
                        };
                        await _daprClient.PublishEventAsync("kafka-pubsub", "thing.description.changes", td, metadata);
                        await SaveToPostgreSqlAsync(td);
                    }
                    catch (Exception ex)
                    {
                        ActorLogger.Error(_thingId, $"Error while background saving TD: {ex}");
                    }
                });
            }
            else
            {
                await SaveToPostgreSqlAsync(td);
            }
        }

        public async Task DeleteAsync()
        {
            try
            {
                await _daprClient.DeleteStateAsync(StateStoreName, ThingDescriptionKey + _thingId);
                ActorLogger.Info(_thingId, "Thing Description deleted from statestore.");
            }
            catch (Exception ex)
            {
                ActorLogger.Error(_thingId, $"Error while deleting ThingDescription from statestore: {ex}");
                throw new InvalidOperationException("Error while deleting ThingDescription from statestore.", ex);
            }

            try
            {
                await using var connection = await _connectionFactory.CreateConnection();
                var cmd = new NpgsqlCommand(
                    "DELETE FROM thing_descriptions WHERE thingId = @ThingId;",
                    connection);
                cmd.Parameters.Add(new NpgsqlParameter("@ThingId", DbType.String) { Value = _thingId });

                int affectedRows = await cmd.ExecuteNonQueryAsync();
                if (affectedRows == 0)
                {
                    ActorLogger.Warn(_thingId, $"No ThingDescription found to delete for ThingId {_thingId}.");
                }
                else
                {
                    ActorLogger.Info(_thingId, "Thing Description deleted from PostgreSQL.");
                }
            }
            catch (Exception ex)
            {
                ActorLogger.Error(_thingId, $"Error while deleting ThingDescription from PostgreSQL: {ex}");
                throw new InvalidOperationException("Error while deleting ThingDescription from PostgreSQL.", ex);
            }
        }

        private async Task<ThingDescription?> LoadFromPostgreSqlAsync()
        {
            await using var connection = await _connectionFactory.CreateConnection();

            var cmd = new NpgsqlCommand(
                "SELECT td FROM thing_descriptions WHERE thingId = @ThingId;",
                connection);
            cmd.Parameters.Add(new NpgsqlParameter("@ThingId", DbType.String) { Value = _thingId });

            await using var reader = await cmd.ExecuteReaderAsync();
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
            await using var connection = await _connectionFactory.CreateConnection();

            var cmd = new NpgsqlCommand(
                    @"INSERT INTO thing_descriptions (thingId, td)
                    VALUES (@ThingId, @NewTd)
                    ON CONFLICT (thingId) DO UPDATE SET td = @NewTd;",
                connection);

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
            catch (JsonException ex) { throw new ArgumentException("Invalid link JSON format.", ex); }

            if (newLink is null) throw new ArgumentException("The link is invalid.");


            ThingDescription!.Links ??= [];
            if (ThingDescription.Links.Any(l => l.Href == newLink.Href && l.Rel == newLink.Rel)) throw new InvalidOperationException("A link with the same Href and Rel already exists.");
            ThingDescription.Links.Add(newLink);
            await SaveAsync(ThingDescription);

            var httpClient = DaprClient.CreateInvokeHttpClient();
            var response = await httpClient.PostAsJsonAsync($"http://twins-service/internal/things/{Uri.EscapeDataString(_thingId)}/links",
                                    JsonSerializer.Serialize(newLink));
            var result = await response.Content.ReadAsStringAsync();

            ActorLogger.Info(_thingId, $"Published add link event for href '{newLink.Href}'.");

            return ThingDescription.ToString()!;
        }

        public async Task<string> UpdateLinkAsync(string targetId, string relName, string linkJson)
        {
            if (ThingDescription is null)
                await LoadAsync();

            if (string.IsNullOrWhiteSpace(linkJson) || string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(relName))
                throw new ArgumentException("Link JSON cannot be null or empty.", nameof(linkJson));

            Link? newLink;
            try
            {
                newLink = JsonSerializer.Deserialize<Link>(linkJson);
            }
            catch (JsonException ex) { throw new ArgumentException("Invalid link JSON format.", ex); }

            if (newLink is null) throw new ArgumentException("The link is invalid.");

            int index = ThingDescription?.Links?.FindIndex(l => l.Href.ToString() == targetId && l.Rel == relName) ?? throw new KeyNotFoundException("Link not found");
            ThingDescription.Links[index] = newLink;
            await SaveAsync(ThingDescription);

            var httpClient = DaprClient.CreateInvokeHttpClient();
            var response = await httpClient.PutAsJsonAsync($"http://twins-service/internal/things/{Uri.EscapeDataString(_thingId)}/links/{relName}/{targetId}",
                                    JsonSerializer.Serialize(newLink));
            var result = await response.Content.ReadAsStringAsync();

            ActorLogger.Info(_thingId, $"Published update link event.");

            return ThingDescription.ToString()!;
        }

        public async Task RemoveLinkAsync(string targetId, string relName)
        {
            if (ThingDescription is null) await LoadAsync();
            if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(relName)) throw new ArgumentException("TargetId or Rel cannot be null or empty.");
            if (ThingDescription?.Links == null || ThingDescription.Links.Count == 0) throw new KeyNotFoundException("No links available.");

            int index = ThingDescription.Links.FindIndex(l => l.Href.ToString() == targetId && l.Rel == relName);
            if (index < 0) throw new KeyNotFoundException($"Link with Href='{targetId}' and Rel='{relName}' not found.");

            ThingDescription.Links.RemoveAt(index);
            await SaveAsync(ThingDescription);

            var httpClient = DaprClient.CreateInvokeHttpClient();
            var response = await httpClient.DeleteAsync($"http://twins-service/internal/things/{Uri.EscapeDataString(_thingId)}/links/{relName}/{targetId}");
            var result = await response.Content.ReadAsStringAsync();

            ActorLogger.Info(_thingId, $"Published delete link event.");
        }

        public async Task<string> AddSubscriptionAsync(string json)
        {
            if (ThingDescription is null)
                await LoadAsync();

            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Subscription JSON cannot be null or empty.", nameof(json));

            SubscribedEvent? newSubscription;
            try
            {
                newSubscription = JsonSerializer.Deserialize<SubscribedEvent>(json);
            }
            catch (JsonException ex)
            {
                throw new ArgumentException("Invalid subscription JSON format.", ex);
            }

            if (newSubscription is null)
                throw new ArgumentException("The subscription is invalid.");

            ThingDescription!.SubscribedEvents ??= [];

            // Avoid duplicates: replace if same target or ID already exists
            var existing = ThingDescription.SubscribedEvents.FirstOrDefault(s => s.Event == newSubscription.Event);
            if (existing != null) ThingDescription.SubscribedEvents.Remove(existing);

            ThingDescription.SubscribedEvents.Add(newSubscription);
            await SaveAsync(ThingDescription);
            ActorLogger.Info(_thingId, $"Added '{newSubscription.Event}' subscription event.");

            return ThingDescription.ToString()!;
        }

        public async Task<string> RemoveSubscriptionAsync(string eventName)
        {
            if (ThingDescription is null)
                await LoadAsync();

            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("Subscription ID cannot be null or empty.");

            if (ThingDescription!.SubscribedEvents is null || ThingDescription.SubscribedEvents.Count == 0)
                throw new KeyNotFoundException($"No subscriptions found for ThingId {_thingId}.");

            var subscription = ThingDescription.SubscribedEvents.FirstOrDefault(s => s.Event == eventName) ?? throw new KeyNotFoundException($"Subscription '{eventName}' not found in ThingId {_thingId}.");

            ThingDescription.SubscribedEvents.Remove(subscription);
            await SaveAsync(ThingDescription);
            ActorLogger.Info(_thingId, $"Published 'removed' subscription event for target '{eventName}'.");

            return ThingDescription.ToString()!;
        }

    }
}
