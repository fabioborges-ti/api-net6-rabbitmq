#nullable disable

using EDA_Customer.Data;
using EDA_Customer.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.RabbitMQ.Interfaces;
using Shared.Settings;
using System.Text;

namespace EDA_Customer.RabbitMq;

public class RabbitMqUtil : IRabbitMqUtil
{
    private readonly RabbitMqSettings _rabbitMqSettings;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public RabbitMqUtil(IServiceScopeFactory serviceScopeFactory, IOptions<RabbitMqSettings> rabbitMqSettings)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _rabbitMqSettings = rabbitMqSettings.Value;
    }

    public async Task PublishMessageQueue(string routingKey, string eventData)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitMqSettings.InstanceName,
            UserName = _rabbitMqSettings.Username,
            Password = _rabbitMqSettings.Password
        };

        var connection = factory.CreateConnection();
        using var channel = connection.CreateModel();

        var body = Encoding.UTF8.GetBytes(eventData);

        channel.BasicPublish(_rabbitMqSettings.TopicExchange, routingKey, null, body);

        await Task.CompletedTask;
    }

    public async Task ListenMessageQueue(IModel channel, CancellationToken cancellationToken)
    {
        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.Received += async (model, ea) =>
        {
            var body = Encoding.UTF8.GetString(ea.Body.ToArray());

            await ParseInventoryProductMessage(body, ea, cancellationToken);
        };

        channel.BasicConsume(_rabbitMqSettings.ProductRoutingKey, true, consumer);

        await Task.CompletedTask;
    }

    private async Task ParseInventoryProductMessage(string message, BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();

        var customerDbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();

        var data = JObject.Parse(message);
        var type = ea.RoutingKey;

        if (type == _rabbitMqSettings.ProductRoutingKey)
        {
            var guidValue = Guid.Parse(data["ProductId"].Value<string>());

            var product = await customerDbContext
                .Products
                .FirstOrDefaultAsync(a => a.ProductId == guidValue, cancellationToken);

            if (product != null)
            {
                product.Name = data["Name"].Value<string>();
                product.Quantity = data["Quantity"].Value<int>();
            }
            else
            {
                await customerDbContext.Products.AddAsync(new Product
                {
                    Id = data["Id"].Value<int>(),
                    ProductId = guidValue,
                    Name = data["Name"].Value<string>(),
                    Quantity = data["Quantity"].Value<int>()
                }, cancellationToken);
            }

            await customerDbContext.SaveChangesAsync(cancellationToken);

            await Task.Delay(new Random().Next(1, 3) * 1000, cancellationToken);
        }
    }
}
