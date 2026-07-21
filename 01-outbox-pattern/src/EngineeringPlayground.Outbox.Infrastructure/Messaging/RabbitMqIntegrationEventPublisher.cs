using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace EngineeringPlayground.Outbox.Infrastructure.Messaging;

public sealed class RabbitMqIntegrationEventPublisher : IIntegrationEventPublisher, IAsyncDisposable
{
    private const string ExchangeName = "engineering-playground.events";
    private readonly ConnectionFactory _connectionFactory;
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public RabbitMqIntegrationEventPublisher(IOptions<RabbitMqOptions> options)
    {
        var settings = options.Value;
        _connectionFactory = new ConnectionFactory
        {
            HostName = settings.Host,
            Port = settings.Port,
            UserName = settings.Username,
            Password = settings.Password
        };
    }

    public async Task PublishAsync(string eventType, string jsonPayload, CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            Persistent = true
        };

        await channel.BasicPublishAsync(
            ExchangeName,
            eventType,
            mandatory: false,
            properties,
            Encoding.UTF8.GetBytes(jsonPayload),
            cancellationToken);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
        {
            return _channel;
        }

        await _initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await _channel.ExchangeDeclareAsync(
                ExchangeName,
                ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
            return _channel;
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _initializationLock.Dispose();
    }
}
