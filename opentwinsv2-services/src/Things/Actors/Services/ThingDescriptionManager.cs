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
using Dapr.Actors.Runtime;

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
            await LoadAsync();
            var existingTd = ThingDescription;
            ThingDescription = td;
            await _daprClient.SaveStateAsync("actorstatestore", ThingDescriptionKey + _thingId, td.ToString());

            //If there was already a ThingDescription, compare types and links
            bool existsInDGraph = false;
            try{
                existsInDGraph = await _daprClient.InvokeMethodAsync<bool>(HttpMethod.Get, "twins-service", $"internal/things/{Uri.EscapeDataString(_thingId)}");
            }catch(InvocationException ex)
            {
                ActorLogger.Info(_thingId, $"Tried to check if it exists in Twins but petition failed: {ex.Message}");
            }

            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true
            };

            if(existingTd is not null)
            {
                //If they are not equal, scrap the existing and override with the new types in dgraph
                var newLinks = td.Links ?? Enumerable.Empty<Link>();
                var oldLinks = existingTd.Links ?? Enumerable.Empty<Link>();

                var linksToDelete = oldLinks.Except(newLinks);
                var linksToAdd = newLinks.Except(oldLinks);

                foreach(var link in linksToDelete)
                    // await RemoveLinkAsync(link.Href.ToString(), link.Rel!);
                    await _daprClient.InvokeMethodAsync(HttpMethod.Delete, "twins-service", $"internal/things/{Uri.EscapeDataString(_thingId)}/links/{link.Rel}/{link.Href}");


                
                Console.WriteLine($"Tengo {linksToAdd.Count()} to add (first: {linksToAdd.FirstOrDefault()})");
                foreach(var link in linksToAdd)
                    // await AddLinkAsync(JsonSerializer.Serialize(link, options));
                    await _daprClient.InvokeMethodAsync(HttpMethod.Post, "twins-service", $"internal/things/{Uri.EscapeDataString(_thingId)}/links", JsonSerializer.Serialize(link));

                var newTypes = td.TypeAnnotation ?? Enumerable.Empty<string>();
                var oldTypes = existingTd.TypeAnnotation ?? Enumerable.Empty<string>();

                var typesToDelete = oldTypes.Except(newTypes);
                var typesToAdd = newTypes.Except(oldTypes);

                if(existsInDGraph)
                    foreach(var type in typesToDelete)
                        await _daprClient.InvokeMethodAsync(HttpMethod.Delete, "twins-service", $"internal/things/{Uri.EscapeDataString(_thingId)}/type/{Uri.EscapeDataString(type)}");
                    foreach(var type in typesToAdd)
                        await _daprClient.InvokeMethodAsync(HttpMethod.Post, "twins-service", $"internal/things/{Uri.EscapeDataString(_thingId)}/type/{Uri.EscapeDataString(type)}");
            }
            else
                foreach(var link in td.Links ?? [])
                    try
                    {
                        if(existsInDGraph)
                            await _daprClient.InvokeMethodAsync(HttpMethod.Post, "twins-service", $"internal/things/{Uri.EscapeDataString(_thingId)}/links", JsonSerializer.Serialize(link));
                            
                    }catch(Exception ex)
                    {
                        ActorLogger.Warn(_thingId, $"Tried to add a link, but failed: {ex.Message}");
                    }
            
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

        public async Task DeleteAsync(bool asyncPersist = true)
        {
            await _daprClient.DeleteStateAsync(StateStoreName, ThingDescriptionKey + _thingId);
            ActorLogger.Info(_thingId, "Thing Description deleted from statestore.");

            ThingDescription = null; //delete it from local memory

            await _daprClient.InvokeMethodAsync(HttpMethod.Delete, "twins-service", $"internal/things/{_thingId}");

            if (asyncPersist)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var metadata = new Dictionary<string, string>()
                        {
                            { "cloudevent.source", new Uri(_thingId).ToString() },
                            { "cloudevent.type", "thing.description.deleted:" + _thingId }
                        };

                        await _daprClient.PublishEventAsync("kafka-pubsub", "thing.description.deleted", _thingId, metadata);
                        await DeleteFromPostgreSqlAsync();
                    }
                    catch (Exception ex)
                    {
                        ActorLogger.Error(_thingId, $"Error while background deleting TD: {ex}");
                    }
                });
            }
            else
            {
                await DeleteFromPostgreSqlAsync();
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

        private async Task DeleteFromPostgreSqlAsync()
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
            Console.WriteLine(1);

            ThingDescription!.Links ??= [];
            if (ThingDescription.Links.Any(l => l.Href == newLink.Href && l.Rel == newLink.Rel)) throw new InvalidOperationException("A link with the same Href and Rel already exists.");
            ThingDescription.Links.Add(newLink);
            Console.WriteLine($"{2}: {ThingDescription}");
            await SaveAsync(ThingDescription);
            // try
            // {
            //     await _daprClient.InvokeMethodAsync(HttpMethod.Post, "twins-service", $"internal/things/{Uri.EscapeDataString(_thingId)}/links", JsonSerializer.Serialize(newLink));
            //     Console.WriteLine(3);
            // }
            // catch (Exception)
            // {
            //      Console.WriteLine($"{3}: ERROR");
            // }
            
           
/*
            using var http = _httpClientFactory.CreateClient();
            var response = await http.PostAsJsonAsync($"{_twinsUrl}internal/things/{Uri.EscapeDataString(_thingId)}/links", JsonSerializer.Serialize(newLink));
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                ActorLogger.Error(_thingId, $"Error body: {error}");
            }
            */
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
            await _daprClient.InvokeMethodAsync(HttpMethod.Put, "twins-service", $"internal/things/{Uri.EscapeDataString(_thingId)}/links/{relName}/{targetId}", JsonSerializer.Serialize(newLink));
/*
            using var http = _httpClientFactory.CreateClient();
            var response = await http.PutAsJsonAsync($"{_twinsUrl}internal/things/{Uri.EscapeDataString(_thingId)}/links/{relName}/{targetId}", JsonSerializer.Serialize(newLink));
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                ActorLogger.Error(_thingId, $"Error body: {error}");
            }*/

            ActorLogger.Info(_thingId, $"Published update link event.");

            return ThingDescription.ToString()!;
        }

        public async Task RemoveLinkAsync(string targetId, string relName)
        {
            if (ThingDescription is null) await LoadAsync();
            if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(relName)) throw new ArgumentException("TargetId or Rel cannot be null or empty.");
            Console.WriteLine(1);
            if (ThingDescription?.Links == null || ThingDescription.Links.Count == 0) throw new KeyNotFoundException("No links available.");

            int index = ThingDescription.Links.FindIndex(l => l.Href.ToString() == targetId && l.Rel == relName);
            Console.WriteLine(2);
            if (index < 0) throw new KeyNotFoundException($"Link with Href='{targetId}' and Rel='{relName}' not found.");

            ThingDescription.Links.RemoveAt(index);
            await SaveAsync(ThingDescription);
            // await _daprClient.InvokeMethodAsync(HttpMethod.Delete, "twins-service", $"internal/things/{Uri.EscapeDataString(_thingId)}/links/{relName}/{targetId}");

/*
            using var http = _httpClientFactory.CreateClient();
            var response = await http.DeleteAsync($"{_twinsUrl}internal/things/{Uri.EscapeDataString(_thingId)}/links/{relName}/{targetId}");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                ActorLogger.Error(_thingId, $"Error body: {error}");
            }
            */

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
