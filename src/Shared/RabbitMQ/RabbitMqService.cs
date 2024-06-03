using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Shared.RabbitMQ.Interfaces;
using Shared.Settings;

namespace Shared.RabbitMQ;

public class RabbitMqService : BackgroundService
{
    private readonly RabbitMqSettings _rabbitMqSettings;
    private readonly IRabbitMqUtil _rabbitMqUtil;
    private readonly IServiceProvider _serviceProvider;

    private IModel _channel;
    private IConnection _connection;

    public RabbitMqService(IServiceProvider serviceProvider, IRabbitMqUtil rabbitMqUtil, IOptions<RabbitMqSettings> rabbitMqSettings)
    {
        _serviceProvider = serviceProvider;
        _rabbitMqUtil = rabbitMqUtil;
        _rabbitMqSettings = rabbitMqSettings.Value;
    }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitMqSettings.InstanceName,
            UserName = _rabbitMqSettings.Username,
            Password = _rabbitMqSettings.Password,
            DispatchConsumersAsync = true
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        return base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine($"{DateTime.Now:u} : Listening the RabbitMq");

        using var scope = _serviceProvider.CreateScope();
        var scopedService = scope.ServiceProvider.GetRequiredService<IRabbitScopedService>();
        await _rabbitMqUtil.ListenMessageQueue(_channel, cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        _connection.Close();
    }
}