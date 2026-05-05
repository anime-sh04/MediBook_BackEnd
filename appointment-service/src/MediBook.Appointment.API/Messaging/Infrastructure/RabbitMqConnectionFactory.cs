using RabbitMQ.Client;

namespace MediBook.Appointment.API.Messaging.Infrastructure;

/// <summary>
/// Singleton factory that creates and caches a single RabbitMQ connection
/// for the lifetime of the application.
///
/// IConnection is thread-safe; IModel (channel) is not — each consumer or
/// publisher creates its own channel from this shared connection.
/// </summary>
public sealed class RabbitMqConnectionFactory : IDisposable
{
    private readonly RabbitMqSettings              _settings;
    private readonly ILogger<RabbitMqConnectionFactory> _logger;
    private IConnection? _connection;
    private readonly object _lock = new();

    public RabbitMqConnectionFactory(
        RabbitMqSettings                   settings,
        ILogger<RabbitMqConnectionFactory> logger)
    {
        _settings = settings;
        _logger   = logger;
    }

    public IConnection GetConnection()
    {
        if (_connection is { IsOpen: true })
            return _connection;

        lock (_lock)
        {
            if (_connection is { IsOpen: true })
                return _connection;

            _logger.LogInformation("[Appointment] Connecting to RabbitMQ...");

            var factory = new ConnectionFactory
            {
                Uri                    = new Uri(_settings.ConnectionString),
                DispatchConsumersAsync = false,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval  = TimeSpan.FromSeconds(10)
            };

            _connection = factory.CreateConnection("MediBook.Appointment");
            _logger.LogInformation("[Appointment] RabbitMQ connection established.");
            return _connection;
        }
    }

    public void Dispose()
    {
        try { _connection?.Close(); } catch { /* ignore */ }
        _connection?.Dispose();
    }
}
