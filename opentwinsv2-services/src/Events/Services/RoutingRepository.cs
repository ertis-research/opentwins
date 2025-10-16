using Npgsql;
using Events.Persistence;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Shared.Constants;

namespace Events.Services
{
    public class RoutingRepository
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly PersistenceBuffer _buffer;
        private readonly ILogger<RoutingRepository> _logger;

        public RoutingRepository(NpgsqlDataSource dataSource, PersistenceBuffer buffer, ILogger<RoutingRepository> logger)
        {
            _dataSource = dataSource;
            _buffer = buffer;
            _logger = logger;
        }

        // --- Lectura directa (sin buffer) ---
        public async Task<List<(string Topic, string Event)>> GetTopicsEventsAsync()
        {
            await using var conn = await _dataSource.OpenConnectionAsync();
            const string sql = @"
                SELECT t.name AS topic, e.name AS event
                FROM topicsEvents te
                JOIN topics t ON t.id = te.topicId
                JOIN events e ON e.id = te.eventId;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            var result = new List<(string, string)>();
            while (await reader.ReadAsync())
                result.Add((reader.GetString(0), reader.GetString(1)));

            return result;
        }

        public async Task<List<(string Event, ActorIdentity Actor)>> GetEventsThingsAsync()
        {
            await using var conn = await _dataSource.OpenConnectionAsync();
            const string sql = @"
                SELECT e.name as eventName, td.thingId
                FROM EventsThings et
                JOIN events e ON e.id = et.eventId
                JOIN thing_descriptions td ON td.id = et.thingId;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            await using var reader = await cmd.ExecuteReaderAsync();

            var result = new List<(string, ActorIdentity)>();
            while (await reader.ReadAsync())
            {
                var evt = reader.GetString(0);
                var actor = new ActorIdentity(reader.GetString(1), Actors.ThingActor);
                result.Add((evt, actor));
            }
            return result;
        }

        // --- Escritura diferida en batch ---

        public async Task LinkTopicEventAsync(string topicName, string eventName)
        {
            const string sql = @"
                INSERT INTO topics(name) VALUES(@topic) ON CONFLICT DO NOTHING;
                INSERT INTO events(name) VALUES(@event) ON CONFLICT DO NOTHING;
                INSERT INTO topicsEvents(topicId, eventId)
                SELECT t.id, e.id
                FROM topics t, events e
                WHERE t.name=@topic AND e.name=@event
                ON CONFLICT DO NOTHING;";

            await _buffer.EnqueueAsync(new PersistenceJob(sql, new()
            {
                new("@topic", NpgsqlTypes.NpgsqlDbType.Text) { Value = topicName },
                new("@event", NpgsqlTypes.NpgsqlDbType.Text) { Value = eventName }
            }));
        }

        public async Task LinkEventThingAsync(string eventName, string thingId)
        {
            const string sql = @"
                INSERT INTO events(name) VALUES(@event) ON CONFLICT DO NOTHING;
                INSERT INTO eventsThings(eventId, thingId)
                SELECT e.id, td.id
                FROM events e, thing_descriptions td
                WHERE e.name=@event AND td.thingId=@thing
                ON CONFLICT DO NOTHING;";

            await _buffer.EnqueueAsync(new PersistenceJob(sql, new()
            {
                new("@event", NpgsqlTypes.NpgsqlDbType.Text) { Value = eventName },
                new("@thing", NpgsqlTypes.NpgsqlDbType.Text) { Value = thingId }
            }));
        }

        public async Task UnlinkTopicEventAsync(string topicName, string eventName)
        {
            const string sql = @"
                DELETE FROM TopicsEvents
                WHERE topicId = (SELECT id FROM topics WHERE name=@topic)
                  AND eventId = (SELECT id FROM events WHERE name=@event);";

            await _buffer.EnqueueAsync(new PersistenceJob(sql, new()
            {
                new("@topic", NpgsqlTypes.NpgsqlDbType.Text) { Value = topicName },
                new("@event", NpgsqlTypes.NpgsqlDbType.Text) { Value = eventName }
            }));
        }

        public async Task UnlinkEventThingAsync(string eventName, string thingId)
        {
            const string sql = @"
                DELETE FROM EventsThings
                WHERE eventId = (SELECT id FROM events WHERE name=@event)
                  AND thingId = (SELECT id FROM thing_descriptions WHERE thingId=@thing);";

            await _buffer.EnqueueAsync(new PersistenceJob(sql, new()
            {
                new("@event", NpgsqlTypes.NpgsqlDbType.Text) { Value = eventName },
                new("@thing", NpgsqlTypes.NpgsqlDbType.Text) { Value = thingId }
            }));
        }
    }
}
