#nullable disable

namespace Shared.Settings;

public class RabbitMqSettings
{
    public string InstanceName { get; set; }
    public string ProductRoutingKey { get; set; }
    public string CustomerRoutingKey { get; set; }
    public string TopicExchange { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
}