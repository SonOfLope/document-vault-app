using DocumentVault.Function.Extensions;
using Microsoft.Extensions.Logging.ApplicationInsights;

namespace DocumentVault.Function
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults(builder =>
                {
                    builder.UseDefaultWorkerMiddleware();
                })
                .ConfigureAppConfiguration(config =>
                {
                    config.AddEnvironmentVariables();
                    config.AddJsonFile("host.json", optional: false);
                })
                .ConfigureLogging((context, builder) =>
                {
                    builder.AddApplicationInsights(
                        configureTelemetryConfiguration: (config) => 
                            config.ConnectionString = context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"],
                        configureApplicationInsightsLoggerOptions: (options) => { }
                    );
                    builder.AddFilter<ApplicationInsightsLoggerProvider>("", LogLevel.Information);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddFunctionServices(context.Configuration);
                    services.AddApplicationInsightsTelemetryWorkerService();
                })
                .Build();

            host.Run();
        }
    }
}
