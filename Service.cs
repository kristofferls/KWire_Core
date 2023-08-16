using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;


namespace KWire
{
    public class Kwire_Service
    {
        private readonly System.Timers.Timer _timer;
        private readonly System.Timers.Timer _ServiceCheckTimer;
        
        
        //private DateTime _lastHeartBeat;
        //public DateTime LastRedlight;
        //private readonly Timer _midnightLogCleanup; //TODO: Delete old logfiles, and print the main menu and current status. 

        public Kwire_Service()

        {
            Core.Setup(); // Call setup-method to initiate program;

            if (Core.AudioDevices.Count != 0 && Core.EGPIs.Count != 0 ) 
            {
                _timer = new System.Timers.Timer(Config.AutoCam_Broadcast_Interval) { AutoReset = true }; // Timer used to call the BroadcastToAutocam function. 
                //_timer.Elapsed += TimerElapsed;
                _timer.Elapsed += UpdateAutoCam;
                Logfile.Write("KWire Service :: AutoCam Broadcast interval in ms is : " + Convert.ToString(Config.AutoCam_Broadcast_Interval));

                if (Config.AudioServiceName != null && Config.ServiceMonitor == true) 
                {
                    
                    /// To be implemented at a later date 16.08.2023. 
                    //_ServiceCheckTimer = new System.Timers.Timer(60000) { AutoReset=true };
                    //_ServiceCheckTimer.Elapsed += CheckServiceStatus;
                }

                Logfile.Write("KWire Service :: Constructor done");
            } 
            else
            {
                Logfile.Write("KWire Service :: Configuration errors found. Please fix! Program terminated");
                DumpRawDataToLog();
                Environment.Exit(1);
            }

            
            

        }

        private async void CheckServiceStatus(object sender, ElapsedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void DumpRawDataToLog() 
        {
            if (Config.Debug == false) 
            {
                Logfile.Write("KWire Service :: Application crashed! To see all memory data, set debug mode on, and restart the application");
            }
            else
            {
                Console.WriteLine("Dumping data");

                Console.WriteLine("Configured number of Audio Devices in Configfile class is " + Config.Devices.Count);
                foreach (var device in Config.Devices) 
                {
                    Console.WriteLine(device.FirstOrDefault()?.ToString());
                }

                Console.WriteLine("Configured number of audio devices in Core class is : " + Core.AudioDevices.Count);

                foreach(var deviceInCore in Core.AudioDevices) 
                {
                    Console.WriteLine(deviceInCore.ToString());
                }

                Console.WriteLine("Configured number of Ember GPIOS is " + Core.EGPIs.Count);

                foreach(var egpio in Core.EGPIs) 
                {
                    Console.WriteLine("EGPIO Name is: " + egpio.Name);   
                }

            }
        
        }
               
        private async void UpdateAutoCam(object sender, ElapsedEventArgs e) 
        {
                       
            if (Config.EmberEnabled) 
            {
                Task broadcast = Core.BroadcastToAutoCam();
                await broadcast.ConfigureAwait(false);
            }
            else
            {
                Task broadcast = Core.BroadcastToAutoCam_NoEmber();
                await broadcast.ConfigureAwait(false);
            }


        }


        public void Start()
        {
            if (Core.AudioDevices.Count != 0 && Core.EGPIs.Count != 0) 
            {
                _timer.Start();
                          
            }
            else 
            {
                Logfile.Write("KWire Service :: Config errors - there is nothing to do but consume CPU time and memory. Please fix KWire_Config.xml");
                Logfile.Write("KWire Service :: This message will repeat in 5 minutes");
                System.Threading.Thread.Sleep(300000);
            }
        }

        public void Stop() 
        {
            _timer.Stop();
              
        }

    }
}
