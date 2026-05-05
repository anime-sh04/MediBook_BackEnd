using RabbitMQ.Client;

namespace MediBook.Payment.API.Messaging.Infrastructure;

/// <summary>
/// Singleton wrapper that provides a retry-backed, auto-recovering
/// IConnection to RabbitMQ / CloudAMQP for the Payment Service.
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
    /// Retries up to 5 times with exponential back-off.
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

    private IConnection CreateConnectionWithRetry()
    {
        const int maxAttempts = 5;
        var delay = TimeSpan.FromSeconds(2);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                _logger.LogInformation(
                    "[Payment] Connecting to RabbitMQ (attempt {Attempt}/{Max})…",
                    attempt, maxAttempts);

                var factory = new ConnectionFactory
                {
                    Uri                      = new Uri(_settings.ConnectionString),
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval  = TimeSpan.FromSeconds(10),
                    RequestedHeartbeat       = TimeSpan.FromSeconds(60),
                    DispatchConsumersAsync   = false
                };

                var conn = factory.CreateConnection("MediBook.Payment");
                _logger.LogInformation("[Payment] Connected to RabbitMQ successfully.");
                return conn;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(ex,
                    "[Payment] RabbitMQ connection failed (attempt {Attempt}/{Max}). " +
                    "Retrying in {Delay}s…",
                    attempt, maxAttempts, delay.TotalSeconds);

                Thread.Sleep(delay);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
            }
        }

        // Final attempt — let exception propagate
        var f = new ConnectionFactory
        {
            Uri                      = new Uri(_settings.ConnectionString),
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval  = TimeSpan.FromSeconds(10),
            RequestedHeartbeat       = TimeSpan.FromSeconds(60)
        };
        return f.CreateConnection("MediBook.Payment");
    }

    public void Dispose()
    {
        try { _connection?.Close(); } catch { /* ignore on shutdown */ }
        _connection?.Dispose();
    }
}
