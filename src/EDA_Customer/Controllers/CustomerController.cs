using EDA_Customer.Data;
using EDA_Customer.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Shared.RabbitMQ.Interfaces;
using Shared.Settings;

namespace EDA_Customer.Controllers;

[ApiController]
[Route("[controller]")]
public class CustomerController : ControllerBase
{
    private readonly CustomerDbContext _customerDbContext;
    private readonly RabbitMqSettings _rabbitMqSettings;
    private readonly IRabbitMqUtil _rabbitMqUtil;

    public CustomerController(CustomerDbContext customerDbContext, IRabbitMqUtil rabbitMqUtil, IOptions<RabbitMqSettings> rabbitMqSettings)
    {
        _customerDbContext = customerDbContext;
        _rabbitMqUtil = rabbitMqUtil;
        _rabbitMqSettings = rabbitMqSettings.Value;
    }

    [HttpGet]
    [Route("/customers")]
    public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers()
    {
        return await _customerDbContext.Customers.ToListAsync();
    }

    [HttpGet]
    [Route("/products")]
    public ActionResult<IEnumerable<Product>> GetProduct()
    {
        return _customerDbContext.Products.ToList();
    }

    [HttpPost]
    public async Task PostCustomer(Customer customer)
    {
        _customerDbContext.Customers.Add(customer);

        await _customerDbContext.SaveChangesAsync();

        var product = JsonConvert.SerializeObject(new
        {
            customer.ProductId,
            TotalBought = customer.ItemInCart
        });

        await _rabbitMqUtil.PublishMessageQueue(_rabbitMqSettings.CustomerRoutingKey, product);
    }
}
