using System.Net;
using System.Net.Mail;
using System.Text;
using test_project.Services.Interfaces;

namespace test_project.Services;

/// <summary>
/// Alerting: пишет в лог/файл и опционально отправляет email (Lab 3).
/// Поддерживает дедупликацию одинаковых CRITICAL-алертов.
/// </summary>
public class AlertService : IAlertService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AlertService> _logger;
    private readonly object _sync = new();
    private string? _lastCriticalFingerprint;
    private bool _lastWasCritical;

    public AlertService(IConfiguration configuration, ILogger<AlertService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendAlertAsync(string subject, string message, CancellationToken ct = default)
    {
        var fingerprint = $"{subject}|{message}";
        lock (_sync)
        {
            if (_lastWasCritical && _lastCriticalFingerprint == fingerprint)
            {
                _logger.LogInformation("Пропуск повторного CRITICAL alert (дедупликация)");
                return;
            }

            _lastWasCritical = true;
            _lastCriticalFingerprint = fingerprint;
        }

        await DeliverAsync("CRITICAL", subject, message, ct);
    }

    public async Task SendRecoveryAsync(string subject, string message, CancellationToken ct = default)
    {
        lock (_sync)
        {
            if (!_lastWasCritical)
            {
                _logger.LogInformation("Recovery не требуется — предыдущее состояние уже OK");
                return;
            }

            _lastWasCritical = false;
            _lastCriticalFingerprint = null;
        }

        await DeliverAsync("RECOVERY", subject, message, ct);
    }

    private async Task DeliverAsync(string level, string subject, string message, CancellationToken ct)
    {
        var payload = $"[{level}] {subject}\n{message}";
        _logger.LogWarning("ALERT {Level}: {Subject}\n{Message}", level, subject, message);

        var alertsDir = Path.Combine(AppContext.BaseDirectory, "alerts");
        Directory.CreateDirectory(alertsDir);
        var file = Path.Combine(alertsDir, $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{level}.txt");
        await File.WriteAllTextAsync(file, payload, Encoding.UTF8, ct);

        var enabled = _configuration.GetValue("Alerts:Email:Enabled", false);
        if (!enabled) return;

        try
        {
            var host = _configuration["Alerts:Email:SmtpHost"] ?? "localhost";
            var port = _configuration.GetValue("Alerts:Email:SmtpPort", 25);
            var from = _configuration["Alerts:Email:From"] ?? "tarot-partitions@localhost";
            var to = _configuration["Alerts:Email:To"] ?? "admin@localhost";
            var user = _configuration["Alerts:Email:Username"];
            var pass = _configuration["Alerts:Email:Password"];

            using var client = new SmtpClient(host, port)
            {
                EnableSsl = _configuration.GetValue("Alerts:Email:UseSsl", false),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            if (!string.IsNullOrWhiteSpace(user))
                client.Credentials = new NetworkCredential(user, pass);

            using var mail = new MailMessage(from, to, $"[{level}] {subject}", message);
            await client.SendMailAsync(mail, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Не удалось отправить email-alert");
        }
    }
}
