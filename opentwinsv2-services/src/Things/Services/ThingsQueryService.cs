using System.Collections.Immutable;
using System.Data;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Things.Infrastructure.Database;
using OpenTwinsV2.Things.Logging;
using OpenTwinsV2.Things.Models;

namespace OpenTwinsV2.Things.Services
{
    public class ThingsQueryService
    {
        private readonly IDbConnectionFactory _connectionFactory;
        private readonly TwinsService _twinsService;
        private readonly EventsService _eventsService;
        private readonly StateService _stateService;
        private readonly StateManagerService _stateManager;

        public ThingsQueryService(IDbConnectionFactory connectionFactory, TwinsService twinsService, EventsService eventsService, StateService stateService, StateManagerService stateManagerService)
        {
            _connectionFactory = connectionFactory;
            _twinsService = twinsService;
            _eventsService = eventsService;
            _stateService = stateService;
            _stateManager = stateManagerService;
        }

        public async Task<PagedResult<ThingDescription>> GetAllThingsAsync(int page, int pageSize, string? searchTerm, bool showConnections)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            if (pageSize > 100) pageSize = 100;

            var offset = (page - 1) * pageSize;

            searchTerm = searchTerm?.Trim();
            bool hasSearch = !string.IsNullOrWhiteSpace(searchTerm);

            await using var connection = await _connectionFactory.CreateConnection();

            string whereClause = "";
            if (hasSearch)
            {
                whereClause = "WHERE (td->>'id' ILIKE @Search OR td->>'title' ILIKE @Search)";
            }

            if (!showConnections)
            {
                var typeFilter = "(td->>'@type' IS NULL OR td->>'@type' NOT LIKE '%Connection%')";

                whereClause = string.IsNullOrWhiteSpace(whereClause) ? $"WHERE {typeFilter}" : $"{whereClause} AND {typeFilter}";
            }

            var countSql = $"SELECT COUNT(*) FROM thing_descriptions {whereClause};";
            using var countCmd = new NpgsqlCommand(countSql, connection);
            if (hasSearch) countCmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");

            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            if (totalCount == 0) return new PagedResult<ThingDescription>([], 0, page, pageSize, 0);

            // 2. Obtener los datos (Aplicando filtro + paginación)
            var dataSql = $@"
                SELECT td 
                FROM thing_descriptions 
                {whereClause}
                ORDER BY id ASC 
                LIMIT @Limit OFFSET @Offset;";

            using var dataCmd = new NpgsqlCommand(dataSql, connection);
            dataCmd.Parameters.AddWithValue("@Limit", pageSize);
            dataCmd.Parameters.AddWithValue("@Offset", offset);
            if (hasSearch) dataCmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");

            var result = new List<ThingDescription>();
            await using var reader = await dataCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var json = reader.GetString(0);
                // Usamos la configuración por defecto o le pasamos opciones si es necesario
                var td = JsonSerializer.Deserialize<ThingDescription>(json);
                if (td != null) result.Add(td);
            }

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return new PagedResult<ThingDescription>(result, totalCount, page, pageSize, totalPages);
        }

        public async Task<PagedResult<ThingSummary>> GetThingsSummaryAsync(int page, int pageSize, string? searchTerm)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 200) pageSize = 200;

            searchTerm = searchTerm?.Trim();
            bool hasSearch = !string.IsNullOrWhiteSpace(searchTerm);
            var offset = (page - 1) * pageSize;

            await using var connection = await _connectionFactory.CreateConnection();

            // Reutilizamos la lógica del WHERE
            string whereClause = "";
            if (hasSearch) whereClause = "WHERE (td->>'id' ILIKE @Search OR td->>'title' ILIKE @Search)";

            // 1. Count (Igual que antes)
            var countSql = $"SELECT COUNT(*) FROM thing_descriptions {whereClause};";
            using var countCmd = new NpgsqlCommand(countSql, connection);
            if (hasSearch) countCmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");

            var totalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            if (totalCount == 0) return new PagedResult<ThingSummary>([], 0, page, pageSize, 0);

            var dataSql = $@"
                SELECT 
                    td->>'id' as id, 
                    td->>'title' as title
                FROM thing_descriptions 
                {whereClause}
                ORDER BY id ASC 
                LIMIT @Limit OFFSET @Offset;";

            using var dataCmd = new NpgsqlCommand(dataSql, connection);
            dataCmd.Parameters.AddWithValue("@Limit", pageSize);
            dataCmd.Parameters.AddWithValue("@Offset", offset);
            if (hasSearch) dataCmd.Parameters.AddWithValue("@Search", $"%{searchTerm}%");

            var result = new List<ThingSummary>();
            await using var reader = await dataCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string id = reader.GetString(0);
                string? title = reader.IsDBNull(1) ? null : reader.GetString(1);

                result.Add(new ThingSummary(id, title));
            }

            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            return new PagedResult<ThingSummary>(result, totalCount, page, pageSize, totalPages);
        }

        public async Task<ThingDescription?> LoadFromPostgreSqlAsync(string id)
        {
            await using var connection = await _connectionFactory.CreateConnection();

            var cmd = new NpgsqlCommand(
                "SELECT td FROM thing_descriptions WHERE thingId = @ThingId;",
                connection);
            cmd.Parameters.Add(new NpgsqlParameter("@ThingId", DbType.String) { Value = id });

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var json = reader.GetString(0);
                var td = JsonSerializer.Deserialize<ThingDescription>(json);
                ActorLogger.Info(id, "Thing Description loaded from PostgreSQL.");
                return td;
            }

            ActorLogger.Info(id, $"No ThingDescription found in PostgreSQL for {id}.");
            return null;
        }

        public async Task SaveToPostgreSqlAsync(ThingDescription td)
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
            if (affected == 0) ActorLogger.Warn(td.Id!, $"No row affected for ThingId {td.Id}");

            ActorLogger.Info(td.Id!, "Thing Description saved in PostgreSQL.");
        }

        public async Task DeleteFromPostgreSqlAsync(string id)
        {
            await using var connection = await _connectionFactory.CreateConnection();
            var cmd = new NpgsqlCommand(
                "DELETE FROM thing_descriptions WHERE thingId = @ThingId;",
                connection);
            cmd.Parameters.Add(new NpgsqlParameter("@ThingId", DbType.String) { Value = id });

            int affectedRows = await cmd.ExecuteNonQueryAsync();
            if (affectedRows == 0)
            {
                ActorLogger.Warn(id, $"No ThingDescription found to delete for ThingId {id}.");
            }
            else
            {
                ActorLogger.Info(id, "Thing Description deleted from PostgreSQL.");
            }
        }

        //bulk insertion instruction --> only one connection and bypassing the actors

        public async Task SaveInBulkToPostgreSqlAsync(IEnumerable<ThingDescription> tds)
        {
            await using var connection = await _connectionFactory.CreateConnection();
            var tasks = tds.Select(async td => 
                {
                    var previousState = await _stateService.LoadThingDescriptionBulkState(td.Id!);
                    return (PreviousState: previousState, New: td);
                }).ToList(); // <-- CRITICAL: Start tasks immediately
                Console.WriteLine(2);
                // 2. Await them all
                var resolvedTasks = await Task.WhenAll(tasks);

                // 3. Deserialize safely and materialize to a List immediately
                var tdPairs = resolvedTasks.Select(pair => 
                {
                    ThingDescription? previousTd = null;
                    try
                    {
                        if (pair.PreviousState is { Count: > 0 } && !string.IsNullOrWhiteSpace(pair.PreviousState[0].Value))
                        {
                            previousTd = JsonSerializer.Deserialize<ThingDescription>(pair.PreviousState[0].Value);
                        }
                    }
                    catch (Exception jsonEx)
                    {
                        Console.WriteLine($"[WARNING] Failed to deserialize previous state for {pair.New.Id}: {jsonEx.Message}");
                    }

                    return (Previous: previousTd, New: pair.New);
                }).ToList(); // <-- CRITICAL: Materialize now so Parallel doesn't evaluate lazily
                Console.WriteLine(3);

                // --- Your PostgreSQL code runs exactly the same ---
            var idsArray = tds.Select(x => x.Id).ToArray(); 
            var tdsArray = tds.Select(x => x.ToString()).ToArray();

            var cmd = new NpgsqlCommand(
                    @"INSERT INTO thing_descriptions (thingId, td)
                    SELECT unnested.id, unnested.td 
                    FROM UNNEST(@ThingIds, @NewTds) AS unnested(id, td)
                    ON CONFLICT (thingId) DO UPDATE 
                    SET td = EXCLUDED.td;",
                connection);

            cmd.Parameters.Add(new NpgsqlParameter("ThingIds", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = idsArray });
            cmd.Parameters.Add(new NpgsqlParameter("NewTds", NpgsqlDbType.Array | NpgsqlDbType.Jsonb) { Value = tdsArray });

            Console.WriteLine(4);

            int affected = await cmd.ExecuteNonQueryAsync();
            if (affected == 0) Console.WriteLine($"No row affected for ThingIds provided");
            Console.WriteLine($"{affected} Thing Descriptions saved in PostgreSQL.");

            // --- Parallel loop is now guaranteed to have solid, non-lazy data ---
            try
            {
                Console.WriteLine($"Starting twins update for {tdPairs.Count} items.");
                
                await Parallel.ForEachAsync(tdPairs, async (pair, cancellationToken) =>
                {                   
                    var td = pair.New;
                    await _stateManager.InitializeStateFromDescription(td.Id!, td.Properties); 
                    
                    if (await _twinsService.ExistsThingInTwins(td.Id!))
                        await _twinsService.UpdateThingInTwins(td, pair.Previous);

                    var events = td.SubscribedEvents?.Select(ev => new EventSubscription(ev.Event, ev.AutoEmitState)).ToList() ?? [];

                    if (events.Count > 0)
                        await _eventsService.SubscribeToEventsAsync(td.Id!, events);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not bulk create in Twins: {ex.Message}");
            }        
        }

        public async Task DeleteInBulkFromPostgreSqlAsync(IEnumerable<string> ids)
        {
            await using var connection = await _connectionFactory.CreateConnection();
            var cmd = new NpgsqlCommand(
                @"DELETE FROM thing_descriptions 
                WHERE thingId = ANY(@ThingIds);",
                connection);
            cmd.Parameters.Add(new NpgsqlParameter("@ThingIds", NpgsqlDbType.Array | NpgsqlDbType.Text) { Value = ids });

            int affectedRows = await cmd.ExecuteNonQueryAsync();
            if (affectedRows == 0)
                Console.WriteLine($"No ThingDescriptions found to delete for the ThingIds provided.");
            
            else
                Console.WriteLine($"{affectedRows} Thing Descriptions deleted from PostgreSQL.");
            
            try
            {
                await Parallel.ForEachAsync(ids, async (id, cancellationToken) =>
                {
                    await _stateManager.DeleteDescriptionStateAsync(id);
                    await _twinsService.DeleteThingInTwins(id);
                });
            }
            catch (Exception)
            {
                Console.WriteLine("Could not bulk delete in Twins");
            }    

            //TODO: Event bulk delete
        }

    }
}