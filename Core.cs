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
         
        public static int NumberOfAudioDevices = WaveIn.DeviceCount;
        public static List<Device> AudioDevices = new List<Device>();
        //public static List<EGPI> EGPIs = new List<EGPI>();
        public static ConcurrentDictionary<string, EGPI> EGPIs { get; set; } = new ConcurrentDictionary<string, EGPI>();
        public static string AudioDevicesJSON;
        //public static int NumberOfGPIs;
        public static WebSocketClient wsclient = new WebSocketClient();
        //public static bool RedlightState = false;
        //public static bool StateChanged = false;
        public static ILogger<EmberConsumerService> emberLogger;
        public static EmberConsumerService emberConsumer; 

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
            emberLogger.LogInformation("EmberConsumerLogger created");

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

        public static void Setup()
        {

            Logfile.Init(); //Start the logger service. 
            Logfile.DeleteOld(); // Deletes old logfiles. 
            Config.ReadConfig(); // Reads all data from XML to Config-class object memory. 



            // Configure Ember-connection. Check to see if the config file contains nothing / blank. To avoid total crash of Ember library. 

            if (Config.Ember_IP == null || Config.Ember_Port == 0)
            {
                Logfile.Write("MAIN :: ERROR :: No valid Ember Provider IP or Port found in Config! Please check KWire_Config.xml");
            }
            else if (Config.EmberEnabled == true)
            {
                ConfigureEGPI();
                emberConsumer = new EmberConsumerService(emberLogger, Config.Ember_IP, Config.Ember_Port);
                emberConsumer.ConfigureEmberConsumer(); //make the connection!
                                                        
            }
            else 
            {
                Logfile.Write("MAIN :: WARN :: Ember is diabled in config - will skip configuration of EGPIOs");
            }
            
            if (Config.AudioServiceName.Length == 0) 
            {
                Logfile.Write("MAIN :: WARN :: AudioService name is not set in KWire_Config.xml");
            }
            else
            {
                AudioService audioService = new AudioService();

                if (audioService.Status() != true) 
                {
                    Logfile.Write("MAIN :: WARN :: AudioService " + Config.AudioServiceName + " is not running!");  
                }
            }
            
            ConfigureAudioDevices();
            DisplayAudioDevices();


            if (Config.AutoCam_IP != null || Config.AutoCam_Port != 0)
            {
                wsclient.Connect(Config.AutoCam_IP, Config.AutoCam_Port); // Connect to nodeJS. 

            }

            else
            {
                Logfile.Write("MAIN :: ERROR :: No valid AutoCam IP or Port found in config! Please check Config.xml");
            }

            JSONExport(); //Create a JSON file that mimics the AutoCam data being sent over websocet. To make debugging AutoCam easier.. 

            Logfile.Write("");
            Logfile.Write("MAIN :: >>>>>>> Startup Complete <<<<<<<<");
        }
        
        public static async Task BroadcastToAutoCam() 
        {
            await Task.Run(() =>
            {
                wsclient.SendJSON(SerializeToJSON());
            });                     

                        
        }

        public static async Task BroadcastToAutoCam_NoEmber()
        {
            /*
            if (Config.Debug) 
            {
                var now = DateTime.Now; 
                Console.WriteLine("Broadcast to AutoCam started @: " + now.ToString());
            }
            */
            // Serialize list of Audiodevices status to JSON: 

            
            await Task.Run(() =>
            {
                var json = (JsonConvert.SerializeObject(AudioDevices));
            

                //Convert JSON-strings to string for modification
                string serializedAudioDevices = json.ToString();
            
                //In order to merge the two strings, some chars added by the Serializer needs to be removed, and others added, so that the two strings can be combined. 
                serializedAudioDevices = serializedAudioDevices.Replace("]", ",");
            

                //Create a new JSONstring of the two different types. 
                string JSONString = serializedAudioDevices;

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

                // Clean up device name for easier matching. This is not used visually, only internally. 

                // Check if the device is a LAWO R3Lay device. Due to the way the device name is written, it is easilly confused. 
                if (dev.ProductName.Contains("LAWO") || dev.ProductName.Contains("Lawo") || dev.ProductName.Contains("R3LAY") ) 
                {                     
                    if (Config.Debug == true) 
                    {
                        Console.WriteLine("Device" + dev.ProductName + " is a LAWO Device - will clean up the dev-name to avoid confusion");
                    }

                    string devNameClean = CleanUpAudioDeviceName(dev.ProductName);
                    
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
                    if (Config.Debug) 
                    {
                        Console.WriteLine("ConfigureAudioDevices() :: Treating device : " + Config.Devices[i][1]);
                        Console.WriteLine("ConfiguredAudioDevices() :: AudioDevices array now has " + AudioDevices.Count + " members");
                    }
                    
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
                        
                        string devNameFromConfig = Convert.ToString(Config.Devices[i][1]); // the device name from position 1 in array. 
                        string sourceName = Config.Devices[i][2];
                        //Clean up the match criteria. 

                        if (!Config.Dante) 
                        {
                            string match = CleanUpAudioDeviceName(devNameFromConfig);


                            //match = Regex.Replace(match, "Lawo R3LAY", "\r\n");
                            //match = match.Replace("(", "".Replace(")", ""));
                            //match = Regex.Replace(match, @"\s+", "");
                            //match = match.ToUpper();

                            for (int x = 0; x < systemDevices.Count; x++)
                            {


                                /*
                                 * The reason why it crashes / does not work
                                 * It should compare the names entered in config, stored in Config.Devices , with washed names (potentially), stored in systemDevices. 
                                 * Currently it gets all the data yet again, and tries to match it. Can of worms...
                                 */

                                //if (Regex.IsMatch(systemDevices[x][1], @"(^|\s)" + match + @"(\s|$)"))

                                //string devName = systemDevices[x][1];
                                //devName = Regex.Replace(devName, "\r\n");

                                if (Config.Debug) 
                                {
                                    Console.WriteLine("Match is " + match);
                                    Console.WriteLine("systemDevices name is: " + systemDevices[x][1]);
                                }


                                if (Regex.IsMatch(systemDevices[x][1].ToUpper(), @"(^|\s)" + match + @"(\s|$)")) 
                                {
                                    var dev = WaveIn.GetCapabilities(x);
                                    AudioDevices.Add(new Device(x, sourceName, dev.ProductName, dev.Channels));
                                }


                            }

                        }

                        if (Config.Dante)
                        {
                            for (int x = 0; x < systemDevices.Count; x++) 
                            {
                                if (systemDevices[x][1].Equals(devNameFromConfig))
                                {
                                    var dev = WaveIn.GetCapabilities(x);
                                    AudioDevices.Add(new Device(x, sourceName, dev.ProductName, dev.Channels));

                                    if (Config.Debug) 
                                    {
                                        Console.WriteLine("Got a device match! Bailing out of loop ");
                                    }
                                    break;
                                }
                            }
                            
                        }
                        

                        if (Config.Debug)
                        {
                            if(Config.Dante) 
                            {
                                Console.WriteLine("Looking for Dante device");
                            }
                            
                            Console.WriteLine("###################################################################");
                            Console.WriteLine("");
                        }


                    }



                }

            }
            else 
            {
                Logfile.Write("MAIN :: WARN :: No devices assigned from CONFIGFILE.");
                if (Config.Debug) 
                {
                    Console.WriteLine("Number of objects in array: " + Config.Devices.Count);
                    foreach(var device in Config.Devices) 
                    {
                        Console.WriteLine(device.Single());
                    }
                }
            }

            
        }

        public static void ConfigureEGPI()
        {
            // Ember-GPIS in config. 

            XmlDocument xml = new XmlDocument();
            xml.Load(Config.ConfigFile);
            string xmlContents = xml.InnerXml;
            xml.LoadXml(xmlContents);



            XmlNodeList eGPIList = xml.SelectNodes("/KWire/EmberGPIs/EGPI");

            if (eGPIList.Count != 0)
            {

                foreach (XmlNode xn in eGPIList)
                {

                    string name;
                    int? id;

                    if (xn["ID"].InnerText.Length == 0)
                    {
                        Logfile.Write("CONFIG :: ERROR :: Found empty EGPI <ID> tag! Discarding");
                        continue;
                    }
                    else
                    {
                        id = Convert.ToInt32(xn["ID"].InnerText);
                    }


                    if (xn["NAME"].InnerText.Length != 0)
                    {
                        name = xn["NAME"].InnerText;
                    }
                    else
                    {
                        name = "N/A";
                    }


                    //Create EGPI Objects and store them in the EGPI object list in Core. If there is an error or blank ID number, dont call the constructor. 


                    if (id != null )
                    {
                        EGPIs.TryAdd(name, new EGPI((int)id, name));
                    }
                    
                }


            }
            else
            {
                Logfile.Write("CONFIG :: WARN :: Found no EGPI tags under <EmberGPIs> ..");
            }


        }
        private static string CleanUpAudioDeviceName(string devName) 
        {
            ///Cleans up the device name coming from the OS. The device name gets trunkated, and needs therefore a cleanup in order to get a match with
            ///what the user has set in the config file. 
            ///Words/phrases to be squashed can be added to the invalidWords array below. 
            ///

            /// Pseudo: 
            /// Get a audio device name string. 
            /// Remove confusion
            ///     - Get rid of all chars including ( if at the end of a string. 
           
            /// Return a more machine comparable name. 


            if(Config.Debug) 
            {
                Console.WriteLine("CleanUpAudioDeviceName: devName is: " +  devName);
            }
            
            string[] invalidWords = { "Lawo", "High Definition", "High", "Realtek"};
            devName = devName.ToUpper();
            string devNameClean = devName;


            foreach ( string word in invalidWords) 
            {

                string pattern = "[()]*" + word.ToUpper() + ".*"; //Will catch (Lawo R3Lay etc. and remove all that is behind it. 
                devNameClean = Regex.Replace(devNameClean, pattern, string.Empty);

            }

            if (Config.Debug)
            {
                Console.WriteLine("CleanUpAudioDeviceName: devNameClean is: " + devNameClean);
            }

            return devNameClean;

        }

        public static void DisplayAudioDevices() 
        {
        
                foreach (var audioDevice in AudioDevices) 
                {
                Logfile.Write("MAIN :: DeviceID:" +audioDevice.DeviceID + " Source: " + audioDevice.Source + " configured");
                }
        
        }
        
        private static string SerializeToJSON()              
        {
            var audioDevicesJSON = (JsonConvert.SerializeObject(AudioDevices));
            var eGPIsJSON = JsonConvert.SerializeObject(EGPIs);

            //Convert JSON-strings to string for modification
            
            string serializedAudioDevices = audioDevicesJSON.ToString();
            string serializedEGPIS = eGPIsJSON.ToString();

            //In order to merge the two strings, some chars added by the Serializer needs to be removed, and others added, so that the two strings can be combined. 
            serializedAudioDevices = serializedAudioDevices.Replace("]", ",");
            serializedEGPIS = serializedEGPIS.Replace("[", "");

            //Create a new JSONstring of the two different types. 
            string JSONString = serializedAudioDevices + serializedEGPIS;

            //Clean up
            JSONString = JSONString += "]";
            JSONString = JSONString.Replace("}]]", "}]");

            return JSONString;

        } 
        private static void JSONExport() 
        {
            
            string ProgramPath = AppDomain.CurrentDomain.BaseDirectory;
            string jSONfilePath = ProgramPath + "JSON-AutoCam-Structure-example.json";
            var audioDevices = new { audioDevices = AudioDevices };

            System.IO.File.WriteAllText(jSONfilePath, SerializeToJSON());

            Logfile.Write("MAIN :: JSONExport :: Wrote: " + jSONfilePath);
        }
  
    }
}
