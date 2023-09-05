using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
//using System.ServiceModel.Description;
//using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Threading;
using System.Text.RegularExpressions;
using Topshelf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Collections.Concurrent;
using KWire_Core;
using System.Data;
using System.Xml;

namespace KWire
{
    public class Core
    {
        public static ILogger<EmberConsumerService> emberLogger;
        public static ILogger<AutoCam> autoCamLogger; 
        public static ILogger<Kwire_Service> kwireLogger;

        static void Main(string[] args)
        {
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
