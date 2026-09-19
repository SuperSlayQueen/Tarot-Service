namespace test_project.Services.Interfaces;

public interface IAlertService
{
    Task SendAlertAsync(string subject, string message, CancellationToken ct = default);
    Task SendRecoveryAsync(string subject, string message, CancellationToken ct = default);
}

public interface IPartitionService
{
    Task<PartitionJobResult> EnsureFuturePartitionsAsync(CancellationToken ct = default);
    Task<PartitionHealthResult> CheckHealthAsync(CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetExistingPartitionsAsync(CancellationToken ct = default);
}

public record PartitionJobResult(
    int ExistingCount,
    int RequiredCount,
    IReadOnlyList<string> Created,
    IReadOnlyList<string> MissingBefore);

public record PartitionHealthResult(
    bool IsHealthy,
    IReadOnlyList<string> Expected,
    IReadOnlyList<string> Missing,
    DateTime CheckedAtUtc);
