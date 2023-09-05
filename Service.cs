using KWire_Core;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using System.Xml;

namespace KWire
{
    public sealed class Kwire_Service
    {
        private readonly System.Timers.Timer _timer;
        private readonly System.Timers.Timer _ServiceCheckTimer;

        public int NumberOfAudioDevices = WaveIn.DeviceCount;
        public List<Device> AudioDevices = new List<Device>();
        public ConcurrentDictionary<string, EGPI> EGPIs { get; set; } = new ConcurrentDictionary<string, EGPI>();
        public string AudioDevicesJSON;
        public EmberConsumerService emberConsumer;
        public AutoCam autoCam;
        private Task updateAutoCam = null;
        private CancellationTokenSource cancellationTokenSource;
        private CancellationToken cancellationToken;
        private ILogger<Kwire_Service> _logger;


        public Kwire_Service(ILogger<Kwire_Service>Logger)

        {
            //Mandatory stuff
            _logger = Logger;
            cancellationTokenSource = new CancellationTokenSource();
            cancellationToken = cancellationTokenSource.Token;


            Logger.LogInformation("KWire_Service created");
            Configure(); // Call setup-method to initiate program;

            if (AudioDevices.Count != 0 && emberConsumer!= null && emberConsumer.EGPIWatchlist.Count != 0 ) 
            {
                _timer = new System.Timers.Timer(Config.AutoCam_Broadcast_Interval) { AutoReset = true }; // Timer used to call the BroadcastToAutocam function. 
                //_timer.Elapsed += TimerElapsed;
                _timer.Elapsed += delegate { UpdateAutoCam(); };

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

        private void UpdateAutoCam() 
        {

            if (autoCam == null)
            {
                _logger.LogWarning("KWire Service :: AutoCam not configured!");  
            }

            else
            {
                Task.Run(async () => await autoCam.UpdateAutoCam(SerializeToJSON()));
            }
        }
       
        
        /*
        private LevelChangedEventHandler UpdateAutoCam(object sender, LevelChangedEventArgs e) 
        {
            Task.Run( () =>  Console.WriteLine("Updating Autocam!" + sender.ToString() + e.ToString())); 
        }
        */
        private async void CheckServiceStatus(object sender, ElapsedEventArgs e)
        {
            throw new NotImplementedException();
        }

        private void DumpRawDataToLog() 
        {
            if (Config.Debug == false) 
            {
                Logfile.Write("KWire Service :: Application crashed! To see all memory data, set debug mode on, and restart the application");
                Logfile.Write(SerializeWhatever(this));
            }
            else
            {
                Console.WriteLine("Dumping data");

                Logfile.Write(SerializeWhatever(this));

                Console.WriteLine("Configured number of Audio Devices in Configfile class is " + Config.Devices.Count);
                foreach (var device in Config.Devices) 
                {
                    Console.WriteLine(device.FirstOrDefault()?.ToString());
                }

                Console.WriteLine("Configured number of audio devices in Servoce is : " + AudioDevices.Count);

                foreach(var deviceInCore in AudioDevices) 
                {
                    Console.WriteLine(deviceInCore.ToString());
                }

                Console.WriteLine("Configured number of Ember GPIOS is " + emberConsumer.EGPIWatchlist.Count);

                foreach(var egpio in EGPIs) 
                {
                    Console.WriteLine("EGPIO Name is: " + egpio.Key);   
                }

            }
        
        }

        public void Start()
        {
            if (AudioDevices.Count != 0) 
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

            Logfile.Write(" --------------- SERIVCE STOPPING --------------");
            _timer.Stop();
            
            foreach (var device in AudioDevices) 
            {
                device.Dispose();

            }

            autoCam.Dispose();

            Logfile.Write(" --------------- PROGRAM TERMINATED --------------");
        }

        public void Configure()
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
                emberConsumer = new EmberConsumerService(Core.emberLogger, Config.Ember_IP, Config.Ember_Port);
                emberConsumer.ConfigureEmberConsumer(); //make the connection!
                ConfigureEGPI();
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
                autoCam = new AutoCam(Core.autoCamLogger, Config.AutoCam_IP, Config.AutoCam_Port, Config.AutoCam_Broadcast_Interval);
            }

            else
            {
                Logfile.Write("MAIN :: ERROR :: No valid AutoCam IP or Port found in config! Please check Config.xml");
            }

            JSONExport(); //Create a JSON file that mimics the AutoCam data being sent over websocet. To make debugging AutoCam easier.. 

            Logfile.Write("");
            Logfile.Write("MAIN :: >>>>>>> Startup Complete <<<<<<<<");
        }


        public void ConfigureAudioDevices()
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
                if (dev.ProductName.Contains("LAWO") || dev.ProductName.Contains("Lawo") || dev.ProductName.Contains("R3LAY"))
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

            if (Config.Devices.Count != 0)
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


                            for (int x = 0; x < systemDevices.Count; x++)
                            {


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
                            if (Config.Dante)
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
                    foreach (var device in Config.Devices)
                    {
                        Console.WriteLine(device.Single());
                    }
                }
            }


        }

        public void ConfigureEGPI()
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


                    if (id != null)
                    {
                        //EGPIs.TryAdd(name, new EGPI((int)id, name));

                        emberConsumer.ConfigureEGPIWatchlist(name, (int)id);
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


            if (Config.Debug)
            {
                Console.WriteLine("CleanUpAudioDeviceName: devName is: " + devName);
            }

            string[] invalidWords = { "Lawo", "High Definition", "High", "Realtek" };
            devName = devName.ToUpper();
            string devNameClean = devName;


            foreach (string word in invalidWords)
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

        public void DisplayAudioDevices()
        {

            foreach (var audioDevice in AudioDevices)
            {
                Logfile.Write("MAIN :: DeviceID:" + audioDevice.DeviceID + " Source: " + audioDevice.Source + " configured");
            }

        }

        //TODO : Cleanup the Serialize JSON stuff - it is moved to class AutoCam. 
        private string SerializeToJSON()
        {
            var audioDevicesJSON = (JsonConvert.SerializeObject(AudioDevices));
            var eGPIsJSON = JsonConvert.SerializeObject(emberConsumer.EGPIWatchlist);

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

        private string SerializeWhatever(object obj) 
        {
            var seralizedObj = JsonConvert.SerializeObject(obj);
            return seralizedObj.ToString();
        }
        private void JSONExport()
        {

            string ProgramPath = AppDomain.CurrentDomain.BaseDirectory;
            string jSONfilePath = ProgramPath + "JSON-AutoCam-Structure-example.json";
            var audioDevices = new { audioDevices = AudioDevices };

            System.IO.File.WriteAllText(jSONfilePath, SerializeToJSON());

            Logfile.Write("MAIN :: JSONExport :: Wrote: " + jSONfilePath);
        }

    }
}
