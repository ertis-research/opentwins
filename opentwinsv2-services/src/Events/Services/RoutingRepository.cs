using System.Data;
using Npgsql;
using OpenTwinsV2.Shared.Constants;
using OpenTwinsV2.Shared.Models;

public class RoutingRepository
{
    private readonly string _connectionString;
    private readonly ILogger<RoutingRepository> _logger;

    public RoutingRepository(IConfiguration configuration, ILogger<RoutingRepository> logger)
    {
        _connectionString = configuration.GetSection("PostgreSQL")["connectionString"]!;
        _logger = logger;
    }

    private async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        _logger.LogDebug("Opening PostgreSQL connection...");
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        _logger.LogDebug("PostgreSQL connection opened successfully.");
        return conn;
    }

    public async Task<List<(string Topic, string Event)>> GetTopicsEventsAsync()
    {
        _logger.LogDebug("Fetching topic-event mappings.");
        using var conn = await OpenConnectionAsync();
        var sql = @"SELECT t.name AS topic, e.name AS event
                    FROM topicsEvents te
                    JOIN topics t ON t.id = te.topicId
                    JOIN events e ON e.id = te.eventId;";

        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        var result = new List<(string, string)>();
        while (await reader.ReadAsync())
        {
            result.Add((reader.GetString(0), reader.GetString(1)));
        }
        _logger.LogDebug("Retrieved {Count} topic-event mappings.", result.Count);
        return result;
    }

    // --- Leer relaciones evento -> actores (things) ---
    public async Task<List<(string Event, ActorIdentity Actor)>> GetEventsThingsAsync()
    {
        _logger.LogDebug("Fetching event-thing mappings.");
        using var conn = await OpenConnectionAsync();
        var sql = @"SELECT e.name as eventName, td.thingId
                    FROM EventsThings et
                    JOIN events e ON e.id = et.eventId
                    JOIN thing_descriptions td ON td.id = et.thingId;";

        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync();

        var result = new List<(string, ActorIdentity)>();
        while (await reader.ReadAsync())
        {
            var evt = reader.GetString(0);
            var actor = new ActorIdentity(reader.GetString(1), Actors.ThingActor);
            result.Add((evt, actor));
        }
        _logger.LogDebug("Retrieved {Count} event-thing mappings.", result.Count);
        return result;
    }

    // --- Guardar vínculos topic <-> event ---
    public async Task LinkTopicEventAsync(string topicName, string eventName)
    {
        _logger.LogDebug("Linking topic '{Topic}' with event '{Event}'.", topicName, eventName);
        using var conn = await OpenConnectionAsync();
        var sql = @"
            INSERT INTO topics(name) VALUES(@topic) ON CONFLICT(name) DO NOTHING;
            INSERT INTO events(name) VALUES(@event) ON CONFLICT(name) DO NOTHING;
            INSERT INTO topicsEvents(topicId, eventId)
            SELECT t.id, e.id
            FROM topics t, events e
            WHERE t.name=@topic AND e.name=@event
            ON CONFLICT DO NOTHING;";

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter("@topic", DbType.String) { Value = topicName });
        cmd.Parameters.Add(new NpgsqlParameter("@event", DbType.String) { Value = eventName });

        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0)
            _logger.LogWarning("No new topic-event link created (topic='{Topic}', event='{Event}').", topicName, eventName);
        else
            _logger.LogDebug("Topic-event link created successfully.");
    }

    // --- Guardar vínculos event <-> actor (thing) ---
    public async Task LinkEventThingAsync(string eventName, string thingId)
    {
        _logger.LogDebug("Linking event '{Event}' with thing '{ThingId}'.", eventName, thingId);
        using var conn = await OpenConnectionAsync();
        var sql = @"
            INSERT INTO events(name) VALUES(@event) ON CONFLICT(name) DO NOTHING;
            INSERT INTO eventsThings(eventId, thingId)
            SELECT e.id, td.id
            FROM events e, thing_descriptions td
            WHERE e.name=@event AND td.thingId=@thing
            ON CONFLICT DO NOTHING;";

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter("@event", DbType.String) { Value = eventName });
        cmd.Parameters.Add(new NpgsqlParameter("@thing", DbType.String) { Value = thingId });

        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0)
            _logger.LogWarning("No new event-thing link created (event='{Event}', thing='{ThingId}').", eventName, thingId);
        else
            _logger.LogDebug("Event-thing link created successfully.");
    }

    public async Task UnlinkTopicEventAsync(string topicName, string eventName)
    {
        _logger.LogDebug("Unlinking topic '{Topic}' from event '{Event}'.", topicName, eventName);
        using var conn = await OpenConnectionAsync();
        var sql = @"
            DELETE FROM TopicsEvents
            WHERE topicId = (SELECT id FROM topics WHERE name=@topic)
                AND eventId = (SELECT id FROM events WHERE name=@event);";

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter("@topic", DbType.String) { Value = topicName });
        cmd.Parameters.Add(new NpgsqlParameter("@event", DbType.String) { Value = eventName });

        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0)
            _logger.LogWarning("No topic-event link found to delete (topic='{Topic}', event='{Event}').", topicName, eventName);
        else
            _logger.LogDebug("Topic-event link deleted successfully.");
    }

    // --- Eliminar vínculo event <-> actor (thing) ---
    public async Task UnlinkEventThingAsync(string eventName, string thingId)
    {
        _logger.LogDebug("Unlinking event '{Event}' from thing '{ThingId}'.", eventName, thingId);
        using var conn = await OpenConnectionAsync();
        var sql = @"
            DELETE FROM EventsThings
            WHERE eventId = (SELECT id FROM events WHERE name=@event)
                AND thingId = (SELECT id FROM thing_descriptions WHERE thingId=@thing);";

        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.Add(new NpgsqlParameter("@event", DbType.String) { Value = eventName });
        cmd.Parameters.Add(new NpgsqlParameter("@thing", DbType.String) { Value = thingId });

        var rows = await cmd.ExecuteNonQueryAsync();
        if (rows == 0)
            _logger.LogWarning("No event-thing link found to delete (event='{Event}', thing='{ThingId}').", eventName, thingId);
        else
            _logger.LogDebug("Event-thing link deleted successfully.");
    }
}
