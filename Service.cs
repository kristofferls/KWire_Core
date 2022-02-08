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
        private readonly System.Timers.Timer _heartBeatTimer;
        
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

                if (Config.Debug == false && Config.HeartBeatEnabled == true) 
                {
                    _heartBeatTimer = new System.Timers.Timer(Config.HeartBeatInterval) { AutoReset = true };
                    _heartBeatTimer.Elapsed += HeartBeatTimer;
                    Logfile.Write("KWire Service :: HeartBeat interval in ms is : " + Convert.ToString(Config.HeartBeatInterval));
                }
                else if (Config.Debug == true)
                {
                    Console.WriteLine("KWire Service :: HeartBeat disabled due to debug mode true");                    
                }
               

                Logfile.Write("KWire Service :: Constructor done");
            } 
            else
            {
                Logfile.Write("KWire Service :: Configuration errors found. Please fix! Program terminated");
                Environment.Exit(1);
            }

            
            

        }
       
        private async void HeartBeatTimer(object sender, ElapsedEventArgs e) 
        {

            
            using (HeartBeat hb = new HeartBeat()) 
            {
                try 
                {

                    DateTime? lastEGPI = hb.LastEGPI(); //Get latest EGPI time.
                    
                    if (lastEGPI != null) 
                    {
                        Logfile.Write("KWire Service :: HeartBeat :: Check Started. Last EGPI was received @ " + lastEGPI.ToString());
                    }
                    else
                    {
                        Logfile.Write("KWire Service :: HeartBeat :: Check Started. No EGPI signal is recorded");
                    }
                    
                    await hb.GetPulse(); //Get latest status from both VPB-service and PowerCore.

                    if (hb.PowerCoreStatus == false)
                    {

                        Logfile.Write("KWire Service :: HeartBeat :: PowerCore not available handling.");
                        //Get serious. Reboot the service.
                        Environment.Exit(1);
                    }

                    if(hb.PowerCoreStatus == true && (hb.AudioServiceStatus == true))
                    {
                        Logfile.Write("KWire Service :: HeartBeat :: All is well!");
                    }
                }

                catch(Exception error) 
                {
                    Logfile.Write("KWire Service :: HEARTBEAT :: " + error.ToString());
                }

            }
        }

        
        private async void UpdateAutoCam(object sender, ElapsedEventArgs e) 
        {
                       
            Task broadcast = Core.BroadcastToAutoCam();
            await broadcast.ConfigureAwait(false);
            /*
            try
            {
                Task.Run(() => { Core.BroadcastToAutoCam() });
                broadcast.Start();
                broadcast.Wait(Config.AutoCam_Broadcast_Interval / 2);
                bool completed = broadcast.IsCompleted;

                if (!completed) 
                {
                    broadcast.Dispose();
                    Logfile.Write("SERVICE :: UpdateAutoCam :: Task did not complete in " + Convert.ToString((Config.AutoCam_Broadcast_Interval / 2)) + " ms!");
                }
                broadcast.Dispose();
            }
            catch (AggregateException)
            {
                Logfile.Write("SERVICE :: UpdateAutoCam ::  EXCEPTION :: ");
                
            } 
            */
            
        }


        public void Start()
        {
            if (Core.AudioDevices.Count != 0 && Core.EGPIs.Count != 0) 
            {
                _timer.Start();
                if (Config.Debug != true && Config.HeartBeatEnabled == true) 
                {
                    _heartBeatTimer?.Start();
                }           
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
            if (Config.Debug != true) 
            {
                _heartBeatTimer?.Stop();   
            }   
        }

    }
}
