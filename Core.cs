using Topshelf;
using Microsoft.Extensions.Logging;
using KWire_Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


namespace KWire
{
    public class Core
    {
        public static ILogger<EmberConsumerService> emberLogger;
        public static ILogger<AutoCam> autoCamLogger; 
        public static ILogger<Kwire_Service> kwireLogger;
        public static ILogger<EGPI> egpiLogger;
        public static ILogger<Core> fileLogger;

        static void Main(string[] args)
        {
            //configure logging. 
            
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder
                    .AddFilter("Microsoft", LogLevel.Warning)
                    .AddFilter("System", LogLevel.Warning)
                    .AddFilter("LoggingConsoleApp.Program", LogLevel.Debug)
                    .AddConsole();
            });

            emberLogger = loggerFactory.CreateLogger<EmberConsumerService>();
            autoCamLogger = loggerFactory.CreateLogger<AutoCam>();
            kwireLogger = loggerFactory.CreateLogger<Kwire_Service>();
            egpiLogger = loggerFactory.CreateLogger<EGPI>();
            fileLogger = loggerFactory.CreateLogger<Core>();

          
            //configure configuration hosting. 

            using IHost host = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args).Build();
            IConfiguration config = host.Services.GetRequiredService<IConfiguration>();


            // TOPSHELF SERVICE
            var exitCode = HostFactory.Run(x =>
                {
                    bool arg = false;

                    // This does not work as intended, as TopShelf looks for the paramter -install, and does not take any further args after installation. To be continued.
                    x.AddCommandLineDefinition("devices", devices => 
                    {

                    });

                    if (!arg) 
                    {
                        x.Service<Kwire_Service>(s => {

                            s.ConstructUsing(kwireService => new Kwire_Service(kwireLogger));
                            s.WhenStarted(kwireService => kwireService.Start());
                            s.WhenStopped(kwireService => kwireService.Stop());

                        });

                        x.RunAsLocalSystem();
                        x.SetServiceName("KWireService");
                        x.SetDisplayName("KWire: Ember+ audiolevel -> AutoCam bridge");
                        x.SetDescription("A service that taps audio inputs and Ember+ messages and translates to AutoCam. Written by kristoffer@nrk.no");

                        x.EnableServiceRecovery(src =>
                        {
                            src.OnCrashOnly();
                            src.RestartService(delayInMinutes: 0); // First failure : Reset immediatly 
                            src.RestartService(delayInMinutes: 1); // Second failure : Reset after 1 minute;
                            src.RestartService(delayInMinutes: 5); // Subsequent failures
                            src.SetResetPeriod(days: 1); //Reset failure conters after 1 day. 
                        });
                    }

                    
                });

                int exitCodeValue = (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
                Environment.ExitCode = exitCodeValue;

        }
  
    }
}
