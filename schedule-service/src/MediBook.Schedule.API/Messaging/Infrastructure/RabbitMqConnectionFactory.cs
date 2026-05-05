using RabbitMQ.Client;

namespace MediBook.Schedule.API.Messaging.Infrastructure;

/// <summary>
/// Provides a lazily-created, retry-backed IConnection to RabbitMQ / CloudAMQP.
///
/// Registered as Singleton so all consumers and publishers share one TCP connection.
/// Uses connection recovery (AutomaticRecoveryEnabled) so transient broker restarts
/// are handled transparently.
/// </summary>
public sealed class RabbitMqConnectionFactory : IDisposable
{
    private readonly RabbitMqSettings                    _settings;
    private readonly ILogger<RabbitMqConnectionFactory> _logger;

    private IConnection? _connection;
    private readonly object _lock = new();

    public RabbitMqConnectionFactory(
        RabbitMqSettings                    settings,
        ILogger<RabbitMqConnectionFactory> logger)
    {
        _settings = settings;
        _logger   = logger;
    }

    /// <summary>
    /// Returns the shared connection, creating it on first call.
    /// Retries up to 5 times with exponential back-off to handle
    /// startup race conditions (service starts before broker is ready).
    /// </summary>
    public IConnection GetConnection()
    {
        if (_connection is { IsOpen: true })
            return _connection;

        lock (_lock)
        {
            if (_connection is { IsOpen: true })
                return _connection;

            _connection = CreateConnectionWithRetry();
        }

        return _connection;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    private IConnection CreateConnectionWithRetry()
    {
        const int maxAttempts = 5;
        var delay = TimeSpan.FromSeconds(2);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "[Schedule] Connecting to RabbitMQ (attempt {Attempt}/{Max})…",
                    attempt, maxAttempts);

                var factory = new ConnectionFactory
                {
                    Uri                        = new Uri(_settings.ConnectionString),
                    AutomaticRecoveryEnabled   = true,   // reconnect after transient failures
                    NetworkRecoveryInterval    = TimeSpan.FromSeconds(10),
                    RequestedHeartbeat         = TimeSpan.FromSeconds(60),
                    DispatchConsumersAsync     = false
                };

                var conn = factory.CreateConnection("MediBook.Schedule");
                _logger.LogInformation("[Schedule] Connected to RabbitMQ successfully.");
                return conn;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex,
                    "[Schedule] RabbitMQ connection failed (attempt {Attempt}/{Max}). " +
                    "Retrying in {Delay}s…",
                    attempt, maxAttempts, delay.TotalSeconds);

                Thread.Sleep(delay);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30)); // exponential cap at 30 s
            }
        }

        // Last attempt (no catch — let it propagate so the host fails fast)
        var factory2 = new ConnectionFactory
        {
            Uri                      = new Uri(_settings.ConnectionString),
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval  = TimeSpan.FromSeconds(10),
            RequestedHeartbeat       = TimeSpan.FromSeconds(60)
        };
        return factory2.CreateConnection("MediBook.Schedule");
    }

    public void Dispose()
    {
        try { _connection?.Close(); }
        catch { /* ignore on shutdown */ }
        _connection?.Dispose();
    }
}
