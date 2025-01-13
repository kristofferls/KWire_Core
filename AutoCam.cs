using KWire;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Activation;

namespace KWire_Core
{
    public class AutoCam : IDisposable
    {
        public IPAddress IPAddress { get; set; }
        public int port { get; set; }   
        public int broadcastInterval {get; set;}

        public bool Connected 
        {
            get;set;
        }
        private WebSocketClient autoCamServer { get; set; }

        private readonly ILogger<AutoCam> _logger;
        public AutoCam(ILogger<AutoCam> logger, IPAddress IP, int Port, int BroadcastInterval) 
        {
            _logger = logger;
            IPAddress = IP;
            broadcastInterval = BroadcastInterval;
            port = Port;
            autoCamServer = new WebSocketClient();

            _logger.LogInformation("Connecting to AutoCam on IP " + IP.ToString() + " Port: " + port);
            autoCamServer.Connect(IPAddress, port);
            if (autoCamServer.Connected == false) 
            {
                int counter = 0;
                while(autoCamServer.Connected == false) 
                {
                    if (counter < 10) 
                    {
                        _logger.LogCritical($"Not connected. Waiting for AutoCam..");
                        Task.Delay(500);
                        autoCamServer.Dispose();
                        autoCamServer = new WebSocketClient();
                        autoCamServer.Connect(IPAddress, port);
                        counter++;
                    } 
                    else 
                    {
                        _logger.LogWarning("AutoCam " + IP.ToString() + " Port: " + port + " Connection failed!!");
                        Connected = false;
                        break;
                    }
                    
                    
                }
                _logger.LogInformation($"Connected to AutoCam? : {autoCamServer.Connected}");
                Connected = true;
            }


        }

        public async Task UpdateAutoCam(string JSON) 
        {
            if (autoCamServer.Connected) 
            {
                await autoCamServer.SendJSON(JSON);
            }
        }

        public async Task UpdateAutoCamNoEmber() 
        {
            throw new NotImplementedException();
        }

        

        public void Dispose()
        {
            autoCamServer.Dispose();
        }
    }

}
