using DocumentVault.Function.Extensions;

namespace DocumentVault.Function
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults()
                .ConfigureAppConfiguration(config =>
                {
                    config.AddEnvironmentVariables();
                    config.AddJsonFile("host.json", optional: true);
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddFunctionServices(context.Configuration);
                })
                .Build();

            host.Run();
        }
    }
}
