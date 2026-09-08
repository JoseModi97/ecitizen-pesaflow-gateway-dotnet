using Ecitizen.PesaflowGateway;
using Ecitizen.PesaflowGateway.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEcitizenPesaflowGateway(builder.Configuration);

var app = builder.Build();

app.UseRouting();
app.MapControllers();

app.Run();
