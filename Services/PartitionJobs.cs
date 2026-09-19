using test_project.Services.Interfaces;

namespace test_project.Services;

/// <summary>
/// Ночной/периодический job создания будущих партиций readings (Lab 3).
/// </summary>
public class CreatePartitionsJob : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CreatePartitionsJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromHours(1);

    public CreatePartitionsJob(IServiceProvider serviceProvider, ILogger<CreatePartitionsJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // небольшой стартовый delay, чтобы БД успела подняться
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var partitions = scope.ServiceProvider.GetRequiredService<IPartitionService>();
                var result = await partitions.EnsureFuturePartitionsAsync(stoppingToken);
                _logger.LogInformation(
                    "CreatePartitionsJob: created={CreatedCount}, missingBefore={Missing}",
                    result.Created.Count, string.Join(',', result.MissingBefore));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "CreatePartitionsJob failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}

/// <summary>
/// Проверка наличия партиций на горизонт + alerting с recovery (Lab 3).
/// </summary>
public class PartitionHealthCheckService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PartitionHealthCheckService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    public PartitionHealthCheckService(IServiceProvider serviceProvider, ILogger<PartitionHealthCheckService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var partitions = scope.ServiceProvider.GetRequiredService<IPartitionService>();
                var alerts = scope.ServiceProvider.GetRequiredService<IAlertService>();
                var health = await partitions.CheckHealthAsync(stoppingToken);

                if (!health.IsHealthy)
                {
                    var message = $"""
                        🚨 Partition alert

                        Table: readings

                        Missing partitions:
                        {string.Join('\n', health.Missing)}

                        Expected horizon: 3 months

                        Checked at:
                        {health.CheckedAtUtc:yyyy-MM-dd HH:mm:ss} UTC
                        """;
                    await alerts.SendAlertAsync("Partition health CRITICAL", message, stoppingToken);
                    _logger.LogWarning("Partition health CRITICAL. Missing: {Missing}", string.Join(',', health.Missing));
                }
                else
                {
                    var message = $"""
                        🟢 Partition check OK

                        Table: readings

                        All required partitions exist.

                        Checked at:
                        {health.CheckedAtUtc:yyyy-MM-dd HH:mm:ss} UTC
                        """;
                    await alerts.SendRecoveryAsync("Partition health OK", message, stoppingToken);
                    _logger.LogInformation("Partition health OK");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "PartitionHealthCheckService failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }
}
