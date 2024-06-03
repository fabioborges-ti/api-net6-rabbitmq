#nullable disable

using EDA_Inventory.Data.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.RabbitMQ.Interfaces;
using Shared.Settings;
using System.Text;

namespace EDA_Inventory.RabbitMq;

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
        cancellationToken.ThrowIfCancellationRequested();

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            Console.WriteLine(" [x] Received {0}", message);

            await ParseCustomerMessageFromTopic(message, ea, cancellationToken);
        };

        channel.BasicConsume(_rabbitMqSettings.CustomerRoutingKey, true, consumer);

        await Task.CompletedTask;
    }

    private async Task ParseCustomerMessageFromTopic(string message, BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var productDbContext = scope.ServiceProvider.GetRequiredService<ProductDbContext>();

        var data = JObject.Parse(message);
        var type = ea.RoutingKey;

        if (type == _rabbitMqSettings.CustomerRoutingKey)
        {
            var guidValue = Guid.Parse(data["ProductId"].Value<string>());

            var product = await productDbContext
                .Products
                .FirstAsync(a => a.ProductId == guidValue, cancellationToken);

            //Get the total Items customer bought
            var itemsBought = data["TotalBought"].Value<int>();
            var currentlyInHand = product.Quantity;
            if (itemsBought <= currentlyInHand)
            {
                var grandTotal = currentlyInHand - itemsBought;
                product.Quantity = grandTotal;
                await productDbContext.SaveChangesAsync(cancellationToken);

                var updatedProduct = JsonConvert.SerializeObject(new
                {
                    product.Id,
                    product.ProductId,
                    product.Name,
                    product.Quantity
                });

                await PublishMessageQueue(_rabbitMqSettings.ProductRoutingKey, updatedProduct);
            }
        }
    }
}
