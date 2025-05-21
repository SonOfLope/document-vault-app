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
