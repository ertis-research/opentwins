using System.Text.Json;
using Npgsql;
using OpenTwinsV2.Shared.Models;
using OpenTwinsV2.Things.Infrastructure.Database;
using OpenTwinsV2.Things.Models;

namespace OpenTwinsV2.Things.Services
{
    public class ThingsQueryService
    {
        private readonly IDbConnectionFactory _connectionFactory;

        public ThingsQueryService(IDbConnectionFactory connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        public async Task<PagedResult<ThingDescription>> GetAllThingsAsync(int page, int pageSize, string? searchTerm)
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

    }
}