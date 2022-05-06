using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace KWire
{
    public class AudioService
    {
        private ServiceController sc;

        public bool Status ()
        {
            bool serviceInstalled = DoesServiceExist(Config.AudioServiceName);

            if (serviceInstalled) //If the Service does exist 
            {
                sc = new ServiceController(Config.AudioServiceName);

                
                    if (sc.Status.Equals(ServiceControllerStatus.Stopped))
                    {
                        Logfile.Write("AUDIOSERVICE :: STATUS :: Service was stopped - trying to start");
                        //Service is not running - start it. 

                        bool status = StartService();

                        if (status != true)
                        {
                            Logfile.Write("AUDIOSERVICE :: WARNING :: AudioService not running - might have fatal consequences!");
                            return false;
                        }
                        else if (status == true)
                        {
                            Logfile.Write("AUDIOSERVICE :: STATUS :: Service started");
                            return true;
                        }
                        
                    }

                    else if (sc.Status.Equals(ServiceControllerStatus.Running))
                    {
                        Logfile.Write("AUDIOSERVICE :: STATUS :: The canary is alive and singing - all is well!");
                        return true;
                    }

                    else if (sc.Status.Equals(ServiceControllerStatus.Paused)) //Rare edge-case should not happen
                    {
                        Logfile.Write("AUDIOSERVICE :: WARNING :: AudioService is paused for some reason..");
                        bool status = StartService();

                        if (status != true)
                        {
                            Logfile.Write("AUDIOSERVICE :: WARNING :: AudioService not running - might have fatal consequences!");
                            return false;
                        }
                        else if (status == true)
                        {
                            Logfile.Write("AUDIOSERVICE :: STATUS :: The canary is alive and singing - all is well!");
                            return true;
                        }

                    }
                    else
                    {
                        return false;
                    }


            }

            else 
            {
                Logfile.Write("AUDIOSERVICE :: ERROR :: Service " + Config.AudioServiceName + " does not exist in current context. Spelled wrong?");
                return false;
            }

            return false;     
        
        }

        private bool DoesServiceExist(string serviceName)
        {
            ServiceController[] services = ServiceController.GetServices("localhost");
            var service = services.FirstOrDefault(s => s.ServiceName == serviceName);
            return service != null;
        }

        private bool StartService() 
        {
            sc.Start();
            if (sc.Status.Equals(ServiceControllerStatus.StartPending) || sc.Status.Equals(ServiceControllerStatus.Stopped)) 
            {
                Logfile.Write("AUDIOSERVICE :: STATUS :: Service starting..");
                

                int counter = 0; 

                while (counter < 10)
                {
                    if (sc.Status.Equals(ServiceControllerStatus.StartPending) )
                    {
                        System.Threading.Thread.Sleep(1000);
                        counter++;
                    }

                    if (sc.Status.Equals(ServiceControllerStatus.Running)) 
                    {
                        return true;
                    }
                }

                return false;

            }
            else
            {
                return false;
            }
            
        }


    }
}
