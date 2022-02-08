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

namespace KWire
{
    public class Core
    {
         
        public static int NumberOfAudioDevices = WaveIn.DeviceCount;
        public static List<Device> AudioDevices = new List<Device>();
        public static List<EGPI> EGPIs = new List<EGPI>();
        public static string AudioDevicesJSON;
        public static int NumberOfGPIs;
        public static WebSocketClient wsclient = new WebSocketClient();
        public static bool RedlightState = false;
        public static bool StateChanged = false;

        static void Main(string[] args)
        {

            

                // TOPSHELF SERVICE
                var exitCode = HostFactory.Run(x =>
                {
                    bool arg = false;

                    // This does not work as intended, as TopShelf looks for the paramter -install, and does not take any further args after installation. To be continued.
                    x.AddCommandLineDefinition("devices", devices => 
                    {
                        Logfile.Init();
                        DisplayAudioDevices();
                        Logfile.Write("AudioDevices written to logfile!");
                        Core.DisplayAudioDevices();
                        arg = true;
                        
                    });

                    if (!arg) 
                    {
                        x.Service<Kwire_Service>(s => {

                            s.ConstructUsing(kwireService => new Kwire_Service());
                            s.WhenStarted(kwireService => kwireService.Start());
                            s.WhenStopped(kwireService => kwireService.Stop());

                        });

                        x.RunAsLocalSystem();
                        x.SetServiceName("KWireService");
                        x.SetDisplayName("KWire: AutoCam AES67 Ember+ Translator");
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

        public static void Setup()
        {

            Logfile.Init(); //Start the logger service. 
            Logfile.DeleteOld(); // Deletes old logfiles. 
            Config.ReadConfig(); // Reads all data from XML to Config-class object memory. 



            if (Config.HeartBeatEnabled == true && Config.Debug == false)
            {
                using (HeartBeat hb = new HeartBeat())
                {

                    while (hb.PowerCoreStatus == false || hb.AudioServiceStatus == false)
                    {
                        Task task = hb.GetPulse();

                        task.Wait();

                        if (hb.AudioServiceStatus == false)
                        {
                            Logfile.Write("MAIN :: WARN :: Audioservice is not running.");

                        }

                        if (hb.PowerCoreStatus == false)
                        {
                            Logfile.Write("MAIN :: FATAL :: PowerCore not responding! Waiting...");

                        }
                    }


                }

            }
            else if (Config.HeartBeatEnabled == false)
            {
                Logfile.Write("MAIN :: INFO :: HeartBeat disabled in KWire_config.xml");
            }



            // Configure Ember-connection. Check to see if the config file contains nothing / blank. To avoid total crash of Ember library. 

            if (Config.Ember_IP == null || Config.Ember_Port == 0)
            {
                Logfile.Write("MAIN :: ERROR :: No valid Ember Provider IP or Port found in Config! Please check KWire_Config.xml");
            }
            else if (Config.EmberEnabled == true)
            {

               EmberConsumer.Connect(); //Connect to the Ember provider
               EmberConsumer.PrintEmberTree(); // Test the connection by printing the deviceTree. 
            }
            else 
            {
                Logfile.Write("MAIN :: WARN :: Ember is diabled in config - will skip configuration of EGPIOs");
            }
            
            
            ConfigureAudioDevices();
            DisplayAudioDevices();

            if (Config.EmberEnabled == true) 
            {
                Config.ConfigureEGPI();
            }
            


            if (Config.AutoCam_IP != null || Config.AutoCam_Port != 0)
            {
                wsclient.Connect(Config.AutoCam_IP, Config.AutoCam_Port); // Connect to nodeJS. 

            }

            else
            {
                Logfile.Write("MAIN :: ERROR :: No valid AutoCam IP or Port found in config! Please check Config.xml");
            }

            //JSONExport(); //Create a JSON file containing all available Device IDS. 

            Logfile.Write("");
            Logfile.Write("MAIN :: >>>>>>> Startup Complete <<<<<<<<");
        }
        public static void TestMethod() 
        {
            var testGPI = new EGPI(1, "REDLIGHT");

            testGPI.GetCurrentState();

            Console.ReadKey();
        
        }
        public static async Task BroadcastToAutoCam() 
        {
            /*
            if (Config.Debug) 
            {
                var now = DateTime.Now; 
                Console.WriteLine("Broadcast to AutoCam started @: " + now.ToString());
            }
            */
            // Serialize both lists of Audio and EGPIs to JSON: 
            await Task.Run(() =>
            {
                var json = (JsonConvert.SerializeObject(AudioDevices));
                var json2 = JsonConvert.SerializeObject(EGPIs);

                //Convert JSON-strings to string for modification
                string serializedAudioDevices = json.ToString();
                string serializedEGPIS = json2.ToString();
                //In order to merge the two strings, some chars added by the Serializer needs to be removed, and others added, so that the two strings can be combined. 
                serializedAudioDevices = serializedAudioDevices.Replace("]", ",");
                serializedEGPIS = serializedEGPIS.Replace("[", "");

                //Create a new JSONstring of the two different types. 
                string JSONString = serializedAudioDevices + serializedEGPIS;

                //Clean up
                JSONString = JSONString += "]";
                JSONString = JSONString.Replace("}]]", "}]");

                //Send to WebSocketClient / NodeJS over dgram / UDP. 
                wsclient.SendJSON(JSONString);

            });                     
            /*
            if (Config.Debug) 
            {
                var now = DateTime.Now;
                Console.WriteLine("Broadcast to AutoCam completed @ " + now.ToString());
            }
            */
                        
        }

        public static void ConfigureAudioDevices()
        {
            // Gets info on all available recording devices, and creates an array of AudioDevice-objects. 

            List<string[]> systemDevices = new List<string[]>(); // temporary storage of all devices. 


            Logfile.Write("MAIN :: There are " + WaveIn.DeviceCount + " recording devices available in this system");
            Logfile.Write("");
            Logfile.Write("----------------- < AUDIODEVICES IN THIS SYSTEM >-----------------------");

            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var dev = WaveIn.GetCapabilities(i);

                Logfile.Write("ID: " + i + " NAME: " + dev.ProductName + " CHANNELS:" + dev.Channels);

                // Cleam up device name for easier matching. This is not used visually, only internally. 

                // Check if the device is a LAWO R3Lay device. Due to the way the device name is written, it is easilly confused. 
                if (dev.ProductName.Contains("LAWO")) 
                {                    
                    if (Config.Debug == true) 
                    {
                        Console.WriteLine("Device" + dev.ProductName + " is a LAWO Device - will clean up the dev-name to avoid ");
                    } 
                    string devNameClean = Regex.Replace(dev.ProductName, "Lawo R3LAY WD", "\r\n"); //Removes the "Lawo R3LAY WD shit from the first device that has no number in its name. 
                    devNameClean = Regex.Replace(devNameClean, "Lawo R3LAY", "\r\n"); //Removes the "Lawo R3LAY shit from the rest of the dev-names that has a number in its name. 
                    devNameClean = devNameClean.Replace("(", ""); // Removes the ( from the string
                    devNameClean = Regex.Replace(devNameClean, @"\s+", ""); //Removes all whitespaces in the name. 

                    string[] adev = { Convert.ToString(i), devNameClean.TrimEnd(), Convert.ToString(dev.Channels) };
                    systemDevices.Add(adev); // Add complete device to list.
                } 

                // If the device is not a Lawo device - continue normally. This would hopefully work on other devices, but might need improving. 
                else 
                
                {
                    string[] adev = { Convert.ToString(i), dev.ProductName.TrimEnd(), Convert.ToString(dev.Channels) };
                    systemDevices.Add(adev); // Add complete device to list.
                }
                
                
            }

            Logfile.Write("-------------------------------------------------");
            Logfile.Write("");

            // Find the deviceID of the devices set in the config. StringCompare. 

            if(Config.Devices.Count != 0 ) 
            {

                for (int i = 0; i < Config.Devices.Count; i++)
                {
                    
                    if (Config.Devices[i][3].Length > 0) // IF there is a DeviceID tag set, use that to create the AudioDevice. 
                    {
                        Logfile.Write("MAIN :: AudioDevice has a defined DeviceID " + Config.Devices[i][3] + " set. Skipping name lookup, and adding directly");
                        
                        int devId = Convert.ToInt32(Config.Devices[i][3]);
                        string srcName = Config.Devices[i][2];
                        
                        var dev = WaveIn.GetCapabilities(devId);
                        AudioDevices.Add(new Device(devId, srcName, dev.ProductName, dev.Channels));
                    }

                    else 
                    {
                        string match = Convert.ToString(Config.Devices[i][1]); // the device name from position 1 in array. 
                        string sourceName = Config.Devices[i][2];
                        //Clean up the match criteria. 

                        match = Regex.Replace(match, "Lawo R3LAY", "\r\n");
                        match = match.Replace("(", "".Replace(")", ""));
                        match = Regex.Replace(match, @"\s+", "");

                        for (int x = 0; x < systemDevices.Count; x++)
                        {

                            if (Regex.IsMatch(systemDevices[x][1], @"(^|\s)" + match + @"(\s|$)"))
                            {
                                var dev = WaveIn.GetCapabilities(x);
                                AudioDevices.Add(new Device(x, sourceName, dev.ProductName, dev.Channels));
                            }

                        }

                    }



                }

            }
            else 
            {
                Logfile.Write("MAIN :: WARN :: No devices assigned from CONFIGFILE.");
            }

            
        }

        public static void DisplayAudioDevices() 
        {
        
                foreach (var audioDevice in AudioDevices) 
                {
                Logfile.Write("MAIN :: DeviceID:" +audioDevice.DeviceID + " Source: " + audioDevice.Source + " configured");
                }
        
        }
        
        private static void JSONExport() 
        {
            
            string ProgramPath = AppDomain.CurrentDomain.BaseDirectory;
            string filePath = ProgramPath + "AudioDevices.json";
            string filePath2 = ProgramPath + "JSONStructure.json";
            var audioDevices = new { audioDevices = AudioDevices };

            var json = (JsonConvert.SerializeObject(audioDevices));
            var json2 = (JsonConvert.SerializeObject(AudioDevices));
            json2 = json2 + (JsonConvert.SerializeObject(EGPIs));
            System.IO.File.WriteAllText(filePath, json);
            System.IO.File.WriteAllText(filePath2, json2);

            Logfile.Write("MAIN :: JSONExport :: Exporting device info to JSON-file: " + filePath);
        }

        

       
    }
}
