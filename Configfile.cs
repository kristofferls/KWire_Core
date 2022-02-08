using System;
using System.IO;
using System.Net;
using System.Xml;
using System.Linq;
using System.Xml.Linq;
using System.Collections.Generic;

namespace KWire
{
    public static class Config
    {
                      
        public static string Ember_IP = null;
        public static int Ember_Port = 0;
        public static IPAddress AutoCam_IP = null;
        public static int AutoCam_Port = 0;
        public static int[] DeviceIDs; // depr. 
        public static bool Debug = false;
        public static string[,] EGPIs;
        public static List<string[]> Devices; 
        public static int Sources;
        public static string Ember_ProviderName;
        private static string ConfigFile;
        public static bool HeartBeatEnabled = false;
        public static int HeartBeatInterval;
        public static int AutoCam_Broadcast_Interval;
        public static string AudioServiceName;
        public static bool EmberEnabled = false;
        static Config() 
        {
            string CurrentDir = AppDomain.CurrentDomain.BaseDirectory;
            string Filename = @"KWire_Config.xml";
            ConfigFile = CurrentDir + @Filename;
        }

        public static void ReadConfig()
        {
            bool emberEnabled = false;
           

            Logfile.Write("CONFIG :: Current XML Config file is: " + ConfigFile);
            Logfile.Write("-----------------------< Start reading config > --------------------------");

            //PARSE SITES.XML
            if (File.Exists(ConfigFile))
            {

                // Read XMl data from sites.xml 
                XmlDocument confFile = new XmlDocument();
                confFile.Load(ConfigFile);

                //Read config-data from xml to temp-vars
                try 
                {
                    if (confFile.DocumentElement != null) 
                    {
                        XmlNode settingsNode = confFile.DocumentElement.SelectSingleNode("Settings");

                        if (settingsNode != null)
                        {
                            
                            XmlNode ember_Enabled = settingsNode.SelectSingleNode("Ember_Enabled");
                            
                            XmlNode serviceMonitor_Enabled = settingsNode.SelectSingleNode("Service_Monitor_Enabled");
                            XmlNode heartBeat_Enabled = settingsNode.SelectSingleNode("HeartBeat_Enabled");
                            
                            XmlNode ember_IP = settingsNode.SelectSingleNode("Ember_IP");
                            XmlNode ember_Port = settingsNode.SelectSingleNode("Ember_Port");
                            XmlNode autoCam_IP = settingsNode.SelectSingleNode("AutoCam_IP");
                            XmlNode autoCam_Port = settingsNode.SelectSingleNode("AutoCam_Port");

                            XmlNode providerName = settingsNode.SelectSingleNode("Ember_ProviderName");
                            XmlNode vpbSericeName = settingsNode.SelectSingleNode("SoundcardService_Name");
                            XmlNode autoCam_BroadcastInterval = settingsNode.SelectSingleNode("AutoCam_BroadcastInterval");
                            XmlNode heartBeat_Interval = settingsNode.SelectSingleNode("HeartBeatInterval");
                            XmlNode audioServiceName = settingsNode.SelectSingleNode("AUDIOSERVICE_NAME");



                            //Resolve IPs and ports and store in memory for later use. Added a lot of ifs to check for errors in formatting, empty tags etc. 

                            //If Ember is enabled - parse the rest of ember related stash. 

                            //TODO: Clean up all ember-stash to be resolved at the same time. 

                            if(ember_Enabled != null && (Convert.ToBoolean(ember_Enabled.InnerXml)) == true) 
                            {
                                EmberEnabled = true; 

                                if (ember_IP.InnerXml.Any() == false || ember_Port.InnerXml.Length == 0 || ember_Port.InnerXml.Any() == false)
                                {
                                    Logfile.Write("CONFIGFILE :: ERROR :: No valid Ember Provider IP or Port found in Config! Please check KWire_Config.xml");

                                }
                                else
                                {
                                    Ember_IP = ember_IP.InnerXml;
                                    Ember_Port = Convert.ToInt32(ember_Port.InnerXml);
                                }
                            }

                            // AutoCam related settings: 

                            if (autoCam_IP.InnerXml.Any() == false || autoCam_Port.InnerXml.Any() == false)
                            {
                                Logfile.Write("CONFIGFILE :: ERROR :: No valid AutoCam IP or Port found in config! Please check Config.xml");

                            }
                            else
                            {
                                AutoCam_IP = Dns.GetHostAddresses(autoCam_IP.InnerXml)[0];
                                AutoCam_Port = Convert.ToInt32(autoCam_Port.InnerXml);
                            }

                            if (EmberEnabled == true) 
                            {
                                if (providerName.InnerXml.Any() == false)
                                {
                                    Logfile.Write("CONFIGFILE :: FATAL :: Ember Provider Name not found in config! Please check KWire_Config.xml! Program terminated");
                                    Environment.Exit(1);
                                }
                                else
                                {
                                    Ember_ProviderName = providerName.InnerXml;
                                }
                            }

                            

                            if (autoCam_BroadcastInterval.InnerXml.Any() == false)
                            {
                                Logfile.Write("CONFIGFILE :: FATAL :: Tag <AutoCam_Broadcast_Interval> not set! Program Terminated");
                                Environment.Exit(1);
                            }
                            else
                            {
                                AutoCam_Broadcast_Interval = Convert.ToInt32(autoCam_BroadcastInterval.InnerXml);
                            }
                            
                            // Heartbeat related settings

                            if(heartBeat_Enabled.InnerXml.Any() == true && (Convert.ToBoolean(heartBeat_Enabled.InnerXml)) == true) 
                            {
                                HeartBeatEnabled = true;

                                if (heartBeat_Interval.InnerXml.Length == 0)
                                {
                                    Logfile.Write("CONFIGFILE :: FATAL :: Tag <HeartBeatInterval> not set!");
                                }
                                else
                                {
                                    HeartBeatInterval = Convert.ToInt32(heartBeat_Interval.InnerXml);
                                }
                            }
                           

                            // Audioservice check - related to HeartBeat. 

                            if (HeartBeatEnabled == true && (Convert.ToBoolean(serviceMonitor_Enabled.InnerXml)) == true) 
                            {
                                if (audioServiceName.InnerXml.Length == 0)
                                {
                                    Logfile.Write("CONFIGFILE :: FATAL :: Tag <AUDIOSERVICE_NAME> not set!");
                                }
                                else
                                {
                                    AudioServiceName = audioServiceName.InnerXml;
                                }
                            } 
                            else if (HeartBeatEnabled == false && (Convert.ToBoolean(serviceMonitor_Enabled.InnerXml)) == true) 
                            {
                                Logfile.Write("CONFIGFILE :: WARN :: ServiceMonitor is enabled, but HeartBeat is disabled. Service monitor will not work.");
                            }
                            else 
                            {
                                AudioServiceName = audioServiceName.InnerXml;
                            }
                            
                        }
                    }
                    

                    

                    
                }
                catch(Exception e) 
                {
                    Console.WriteLine("ConfigFile error: " + e.ToString());
                }
                

                // Ember-GPIS in config. 

                XmlDocument xml = new XmlDocument();
                xml.Load(ConfigFile);
                string xmlContents = xml.InnerXml;
                xml.LoadXml(xmlContents);


                //Get all <DEVICE>-tags, and put them into a string array of IDs.

                Devices = new List<string[]>();

                int numOfDevInCfg = XDocument.Load(ConfigFile).Descendants("Device").Count(); //Number of devices in file. 

                if(numOfDevInCfg != 0) 
                {
                    Sources = numOfDevInCfg; // update global number of devices. Used by Core. 
                    XmlNodeList aDevices = xml.SelectNodes("/KWire/AudioDevices/Device");

                    foreach (XmlNode xn in aDevices)
                    {
                        string order = xn["ORDER"].InnerText;
                        string name = xn["NAME"].InnerText;
                        string source = xn["SOURCE"].InnerText;
                        string devID = xn["DEVICE_ID"].InnerText;

                        if (devID.Length != 0 || name.Length != 0) // Check if the data is valid. You can either enter a Device_ID OR Device name. If none are set, the XML-entry is discarded.
                        {
                            string[] devs = { order, name, source, devID };
                            Devices.Add(devs);
                        }
                        else if (name.Length == 0 && devID.Length == 0)
                        {
                            Logfile.Write("CONFIG :: Got an empty <NAME> and <DEVICE_ID> tag - discarded");
                            continue;
                        }

                        if (devID.Length != 0) //If there is a Device_ID tag in config, make a note of this. 
                        {
                            Logfile.Write("CONFIG :: Added: <" + name + "> with source: " + source + " and order: " + order + " to device list");
                            Logfile.Write("CONFIG :: " + name + " has a DeviceID " + devID + " set in config. This will override name search");
                        }

                        else 
                        {
                            Logfile.Write("CONFIG :: Added: <" + name + "> with source: " + source + " and order: " + order + " to device list");
                        }
                        
                    }

                    Logfile.Write("CONFIG :: Found " + Convert.ToString(Devices.Count) + " audio inputs in config file.");
                } 
                else 
                {
                    Logfile.Write("CONFIG :: WARN :: Found no <DEVICE> tags under <AudioDevices>.. did you forget?");

                }


                //Get all <EGPI>-tags, and put them into a temp List of objects.


                
                
                //Parse Debug-settings

                XmlNode settingsXmlNode = confFile.DocumentElement.SelectSingleNode("Settings");
                XmlNode debug = settingsXmlNode.SelectSingleNode("Debug");

                if (debug.InnerXml.Length != 0)
                {
                    string debugNode = debug.InnerXml;
                    debugNode = debugNode.ToLower();

                    Debug = Convert.ToBoolean(debug.InnerXml);
                    /*

                    if (debugNode.Contains("false"))
                    {
                        Debug = false;
                        Logfile.Write("CONFIG :: Debug is currently not enabled.");
                    }
                    if (debugNode.Contains("true"))
                    {
                        Debug = true;
                        Logfile.Write("CONFIG :: Debug is set to ON.");
                    }
                    */
                }
                else
                {
                    Logfile.Write("CONFIG :: Debug tag not found in config file!");
                    Debug = false;
                }

                Logfile.Write("-----------------------< Finished reading config > --------------------------");

                
                if (emberEnabled == true) 
                {
                    ConfigureEGPI(); //Converted to method as the same feature is used by HeartBeat to restart the show if something fails
                }
                
            }
            else
            {
                Logfile.Write("CONFIG :: Cannot read config file! Please make sure there is a KWire_Config.xml in the install dir");
                Environment.Exit(1);
            }// END OF XML parsing. 
        }

        public static void ConfigureEGPI() 
        {
            // Ember-GPIS in config. 
            
            XmlDocument xml = new XmlDocument();
            xml.Load(ConfigFile);
            string xmlContents = xml.InnerXml;
            xml.LoadXml(xmlContents);



            XmlNodeList eGPIList = xml.SelectNodes("/KWire/EmberGPIs/EGPI");

            if (eGPIList.Count != 0)
            {


                EGPIs = new string[eGPIList.Count, 2];


                foreach (XmlNode xn in eGPIList)
                {

                    string name = null;
                    int? id = null;

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
                        int parsedID = id.Value;
                        Core.EGPIs.Add(new EGPI(parsedID, name));
                    }
                    System.Threading.Thread.Sleep(1000); // Give the PowerCore some time to think before hammering it again... 
                }


            }
            else
            {
                Logfile.Write("CONFIG :: WARN :: Found no EGPI tags under <EmberGPIs> ..");
            }

            
        }

    }
}
