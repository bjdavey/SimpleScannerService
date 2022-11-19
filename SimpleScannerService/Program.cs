var builder = WebApplication.CreateBuilder(args);
var baseUrl = builder.Configuration.GetValue<string>("baseUrl");

builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "Simple.Scanner.Service";
});
builder.Services.AddControllers();

if (!string.IsNullOrEmpty(baseUrl))
    builder.WebHost.UseUrls(baseUrl);

var app = builder.Build();

//app.UseHttpsRedirection();

//app.UseAuthorization();

app.MapControllers();

app.Run();
