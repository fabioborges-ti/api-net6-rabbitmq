using EDA_Customer.Data.Context;
using EDA_Customer.RabbitMq;
using Microsoft.EntityFrameworkCore;
using Shared.Extensions;
using Shared.RabbitMQ;
using Shared.RabbitMQ.Interfaces;
using Shared.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMq"));

builder.Services.AddDbContext<CustomerDbContext>(options => options.UseSqlite(@"Data Source=customer.db"));

builder.Services
    .AddSingleton<IRabbitMqUtil, RabbitMqUtil>()
    .UseRabbitMqSettings()
    .AddScoped<IRabbitScopedService, RabbitScopeService>()
    .AddHostedService<RabbitMqService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CustomerDbContext>();
    dbContext.Database.EnsureCreated();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
