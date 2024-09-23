using System.Net;
using System.Text.Json.Serialization;

namespace KWire_Console.Models
{
    public class AppSettings
    {
        public class KWire
        {
            public Settings Settings { get; set; }
            public List<AudioDevice> AudioDevices { get; set; }
            public List<EGPI> EmberGPIs { get; set; }
            public List<AutoCam> AutoCams { get; set; }
            public List<EmberProviders> EmberProviders { get; set; }
        }

        public class Settings
        {
            public bool Ember_Enabled { get; set; }
            public string AutoCam_IP { get; set; }

            [JsonIgnore]
            public IPAddress AutoCam_Host => Dns.GetHostAddresses(AutoCam_IP ?? "127.0.0.1")[0]; //move this somewhere more appropriate maybe? 

            public bool Service_Monitor_Enabled { get; set; }
            public string AudioService_Name { get; set; }
            public bool DHD { get; set; }
            public bool Dante { get; set; } //to be deprecated. Should be supported out of the box, without need for further configuration. 
            public bool Debug { get; set; }

            public bool AutoRecEnabled { get; set; }
            public string AutoRecPath { get; set; }
            public string AutoRecTempPath { get; set; }
            public int WebServerPort { get; set; }

        }

        public class AudioDevice
        {
            public string DEVICE_ID { get; set; }
            public string NAME { get; set; }
            public string SOURCE { get; set; }
            public int ORDER { get; set; }
        }

        public class EGPI
        {
            public int ID { get; set; }
            public string NAME { get; set; }
            public string URL_true { get; set; }   
            public string URL_false { get; set; }
        }

        public class AutoCam 
        {
            public IPAddress IPAddress { get; set; }
            public int Port { get; set; }
            public int BroadcastInterval { get; set; }
        }

        public class EmberProviders
        {
            public string EmberProviderIP { get; set; }
            public int EmberProviderPort { get; set; }
            public string MakerBrand { get; set; } //in order to support both Lawo and DHD providers. They have different tree-strutures. 
            public string Ember_GPONodeName { get; set; } //in case of Lawo - this name can be set in the mixer config. 
        }
    }

    
}