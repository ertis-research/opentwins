using System.Threading.Channels;
using Npgsql;

namespace Events.Persistence
{
    public record PersistenceJob(string Sql, List<NpgsqlParameter> Parameters);
    public class PersistenceBuffer : BackgroundService
    {
        private readonly Channel<PersistenceJob> _queue = Channel.CreateUnbounded<PersistenceJob>();
        private readonly NpgsqlDataSource _dataSource;
        private readonly ILogger<PersistenceBuffer> _logger;

        private const int MaxBatchSize = 200;       // operaciones por lote
        private const int FlushIntervalMs = 2000;   // flush cada 2 segundos

        public PersistenceBuffer(NpgsqlDataSource dataSource, ILogger<PersistenceBuffer> logger)
        {
            _dataSource = dataSource;
            _logger = logger;
        }

        public async Task EnqueueAsync(PersistenceJob job)
        {
            await _queue.Writer.WriteAsync(job);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var buffer = new List<PersistenceJob>();
            var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(FlushIntervalMs));

            _logger.LogInformation("PersistenceBuffer started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                while (buffer.Count < MaxBatchSize && _queue.Reader.TryRead(out var job))
                    buffer.Add(job);

                if (buffer.Count == 0)
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                    continue;
                }

                await FlushAsync(buffer, stoppingToken);
                buffer.Clear();
            }
        }

        private async Task FlushAsync(List<PersistenceJob> jobs, CancellationToken token)
        {
            try
            {
                await using var conn = await _dataSource.OpenConnectionAsync(token);
                await using var tx = await conn.BeginTransactionAsync(token);

                foreach (var job in jobs)
                {
                    await using var cmd = new NpgsqlCommand(job.Sql, conn, tx);
                    cmd.Parameters.AddRange(job.Parameters.ToArray());
                    await cmd.ExecuteNonQueryAsync(token);
                }

                await tx.CommitAsync(token);
                _logger.LogInformation("Flushed {Count} persistence operations", jobs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing persistence jobs");
            }
        }
    }
}
