using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.ServiceProcess;
using System.Net.Http;
using System.Net;

namespace KWire
{
    public class HeartBeat : IDisposable
    {
        private ServiceController sc { get; set; }
        public bool PowerCoreStatus { get; set; } = false;
        public bool AudioServiceStatus { get; set; } = false;

        private DateTime powerCoreLastSeen = new DateTime();
        public HeartBeat() 
        {
            //Constructor

            

            if (Config.AudioServiceName.Any()) 
            {
                try
                {
                    sc = new ServiceController(Config.AudioServiceName);
                    
                }
                catch (Exception error)
                {
                    Logfile.Write("HeartBeat :: ERROR :: " + error);
                }

            }

            
        }
        void IDisposable.Dispose()
        {
            //Does this work..? 
        }

        public async Task GetPulse() 
        {
            //Run check service routine. 
            Task serviceStatus = VPB_ServiceCheck();
            await serviceStatus;

            //Run the PowerCoreCheck-routine.

            bool response = await PowerCoreHTTPStatus();

            if (response == true) 
            {
                Logfile.Write("HeartBeat :: PowerCore responded OK");
                PowerCoreStatus = true;
                powerCoreLastSeen = DateTime.Now;
            }                                 
            else 
            {
                Logfile.Write("HeartBeat :: PowerCore did not respond! It was last seen " + powerCoreLastSeen.ToString());
                PowerCoreStatus = false;
                Logfile.Write("HeartBeat :: FATAL :: PowerCore unavailable!");
                //await Task.Delay(10000);

                //If the PowerCore somehow has not responded to the HTTP-request, it is more than likely that all Ember-connections are sewered. Therefore, the service must restart. 

                // Check again 10 times before doing anything. 

            }

        }

        public async Task VPB_ServiceCheck() 
        {

            // Get status 

           
            bool currentStatus = await ServiceStatus(); //True is OK false is not ok.

            if (Config.Debug) { Console.WriteLine("VPB_SERVICECHECK currentStatus is ::" + currentStatus.ToString()); }

            if (currentStatus) 
            {
                Logfile.Write("HeartBeat :: Service " + Config.AudioServiceName + " is OK");
                AudioServiceStatus = true;
            
            }

            if (!currentStatus) 
            {
                Logfile.Write("HeartBeat :: ERROR :: Service " + Config.AudioServiceName + " is not running!");

                if (sc != null) 
                {
                    await StartService();

                    await Task.Delay(10000);

                    bool newStatus = Convert.ToBoolean(ServiceStatus());

                    int counter = 0;

                    if (!newStatus)
                    {
                        while (counter < 10 || !newStatus)
                        {
                            Logfile.Write("HeartBeat :: ERROR :: Service was tried started, but failed to start. This was try nr." + counter);

                            if (sc.Status == ServiceControllerStatus.StartPending)
                            {
                                await Task.Delay(10000);
                                newStatus = Convert.ToBoolean(ServiceStatus());
                                AudioServiceStatus &= newStatus;
                            }
                            else
                            {
                                await StartService();
                                await Task.Delay(10000);
                                newStatus = Convert.ToBoolean(ServiceStatus());
                                AudioServiceStatus &= newStatus; 

                            }

                            counter++;
                        }
                    }
                    else
                    {
                        AudioServiceStatus = true;
                    }

                    if (newStatus)
                    {
                        Logfile.Write("HeartBeat :: Service :: Service " + Config.AudioServiceName + " started successfully");

                    }
                } 

                

            }
                

                
        }


        public async Task<Boolean> PollPowerCore() 
        {
                       
            bool response = await PowerCoreHTTPStatus();

            if (Config.Debug)
            {
                Console.WriteLine("HeartBeat :: PollPowerCore called!");
                Console.WriteLine("HeartBeat :: Response was : " + response.ToString());
            }

            bool result;

            if (response == true)
            {
                result = true;
                PowerCoreStatus = true;
            }
            else
            {
                result = false;
                PowerCoreStatus = false;
            }

            return result;
        }

        private async Task<Boolean> PowerCoreHTTPStatus() //Poll HTTP: If site is unavailable - consider all Ember and audio connections to be void. 
        {
            string url = "http://" + Config.Ember_IP + "/";

            if (Config.Debug) { Console.WriteLine("url string is " + url); }
            

            try
            {
                using (HttpClient httpClient = new HttpClient())
                using (HttpResponseMessage response = await httpClient.GetAsync(url))
                {
                    //string responseBody = await response.Content.ReadAsStringAsync();             
                                
                    if (response != null) 
                    {
                        if (Config.Debug)
                        {
                            Console.WriteLine(response);
                        }
                        
                        if(response.StatusCode == HttpStatusCode.OK) 
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }

                    }
                    else
                    {
                        Logfile.Write("HeartBeat :: PowerCoreHTTPStatus :: Response was null!");
                        throw new Exception(response.ToString());
                    }
                }
            }
            catch (Exception error)
            {
                Logfile.Write("HeartBeat :: PowerCoreHTTPStatus :: " + error.ToString());
                
            }
            //await Task.Run(()=>0);
            return false;            
        }

        private async Task<Boolean> ServiceStatus() 
        {
            if (sc != null) 
            {
                await Task.Run(() => sc.Refresh());


                if (sc.Status == ServiceControllerStatus.Stopped)
                {
                    return false;
                }

                if (sc.Status == ServiceControllerStatus.StartPending)
                {
                    Logfile.Write("HeartBeat :: ServiceStatus : Start Pending");
                    return false;
                }

                if (sc.Status == ServiceControllerStatus.StopPending)
                {
                    Logfile.Write("HeartBeat :: ServiceStatus : Stop Pending");
                    return false;
                }

                if (sc.Status == ServiceControllerStatus.Running)
                {

                    return true;
                }

                else
                {
                    return false;
                }
            }
            else
            {
                Logfile.Write("HeartBeat :: Cannot get ServiceStatus when no service name was provided");
            }
            return false;    
            
        }

        private async Task StartService() 
        {
            if (sc != null) 
            {
                await Task.Run(() => sc.Refresh());

                await Task.Run(() => sc.Start());

                Logfile.Write("HeartBeat :: Attempting to start Service: " + Config.AudioServiceName);
            }

        }
        
        private async Task StopService() 
        {
            if (sc != null) 
            {
                await Task.Run(() => sc.Refresh());

                Logfile.Write("HeartBeat :: Attempting to start Service: " + Config.AudioServiceName);

                await Task.Run(() => sc.Stop());
            }


        }

        private async Task RestartService() 
        {
            
           await StopService();
           await StartService();
            
        }

        public DateTime? LastEGPI() //Get all LastChange times from all known EGPI objects. If there are any.
        {

            
            DateTime now = DateTime.Now;

            
            DateTime? latest = Core.EGPIs.Min(r => r.LastChange); // Nullable DateTime Linq <3

            if (latest != null) 
            {

                //TimeSpan ts = now - Convert.ToDateTime(latest);

                //DateTime lastEGPI = Convert.ToDateTime(ts);

                if (Config.Debug) 
                {
                    Console.WriteLine("LastEGPI was " + Convert.ToString(latest));
                }

                return latest;
            }
            else
            {
                return null;
            }


        }
       









    }







}
