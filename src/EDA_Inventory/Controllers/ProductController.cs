using EDA_Inventory.Data;
using EDA_Inventory.Data.Context;
using Microsoft.AspNetCore.Mvc;
using Shared.RabbitMQ.Interfaces;
using Shared.Settings;
using System.Text.Json;

namespace EDA_Inventory.Controllers;

[ApiController]
[Route("[controller]")]
public class ProductController : ControllerBase
{
    private readonly ProductDbContext _productDbContext;
    private readonly RabbitMqSettings _rabbitMqSettings;
    private readonly IRabbitMqUtil _rabbitMqUtil;

    public ProductController(ProductDbContext productDbContext, IRabbitMqUtil rabbitMqUtil, RabbitMqSettings rabbitMqSettings)
    {
        _productDbContext = productDbContext;
        _rabbitMqUtil = rabbitMqUtil;
        _rabbitMqSettings = rabbitMqSettings;
    }

    [HttpGet]
    public ActionResult<IEnumerable<Products>> GetProducts()
    {
        return _productDbContext.Products.ToList();
    }

    [HttpPut]
    public async Task<ActionResult<Products>> UpdateProduct(Products products)
    {
        _productDbContext.Products.Update(products);

        await _productDbContext.SaveChangesAsync();

        var product = JsonSerializer.Serialize(new
        {
            products.Id,
            NewName = products.Name,
            products.Quantity
        });

        await _rabbitMqUtil.PublishMessageQueue(_rabbitMqSettings.ProductRoutingKey, product);


        return CreatedAtAction("GetProducts", new { products.Id }, products);
    }

    [HttpPost]
    public async Task PostProduct(Products products)
    {
        products.ProductId = Guid.NewGuid();

        _productDbContext.Products.Add(products);

        await _productDbContext.SaveChangesAsync();

        var product = JsonSerializer.Serialize(new
        {
            products.Id,
            products.ProductId,
            products.Name,
            products.Quantity
        });

        await _rabbitMqUtil.PublishMessageQueue(_rabbitMqSettings.ProductRoutingKey, product);
    }
}
