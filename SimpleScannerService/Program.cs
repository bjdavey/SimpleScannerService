using NLog;
using NLog.Web;

var logger = NLog.LogManager.Setup().LoadConfigurationFromAppSettings().GetCurrentClassLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
    builder.Host.UseNLog();

    builder.Host.UseWindowsService(options =>
    {
        options.ServiceName = "Simple.Scanner.Service";
    });

    builder.Services.AddControllers();

    var baseUrl = builder.Configuration.GetValue<string>("baseUrl");
    if (!string.IsNullOrEmpty(baseUrl))
        builder.WebHost.UseUrls(baseUrl);

    var app = builder.Build();

    app.MapControllers();

    app.UseCors(builder => builder
                               .AllowAnyOrigin()
                               .AllowAnyMethod()
                               .AllowAnyHeader());

    app.Run();
}
catch (Exception ex)
{
    logger.Error(ex, "Stopped program because of exception");
    throw;
}
finally
{
    NLog.LogManager.Shutdown();
}