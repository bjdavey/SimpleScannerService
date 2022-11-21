var builder = WebApplication.CreateBuilder(args);
var baseUrl = builder.Configuration.GetValue<string>("baseUrl");

builder.Services.AddControllers();

if (!string.IsNullOrEmpty(baseUrl))
    builder.WebHost.UseUrls(baseUrl);

var app = builder.Build();

app.MapControllers();

app.Run();
