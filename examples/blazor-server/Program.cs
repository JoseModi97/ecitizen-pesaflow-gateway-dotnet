using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.AspNetCore;
using Ecitizen.PesaflowGateway.Models;

var builder = WebApplication.CreateBuilder(args);

// Register eCitizen services
builder.Services.AddEcitizenPesaflowGateway(builder.Configuration);

var app = builder.Build();

// Endpoint demonstrating server-side checkout generation in Blazor / Razor application
app.MapGet("/blazor/checkout-data", (EcitizenClient client) =>
{
    var checkout = client.Checkout(new PaymentInput
    {
        Amount = 750.00m,
        Reference = $"BLAZOR-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
        Description = "Portal Subscription",
        Name = "John Doe",
        IdNumber = "12345678",
        Phone = "0712345678",
        SendStkPush = false
    });

    return Results.Ok(new
    {
        checkout.Url,
        checkout.Payload
    });
});

app.Run();
