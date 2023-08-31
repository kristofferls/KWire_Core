using KWire_Core;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.Security.EnterpriseData;
using Windows.ApplicationModel.VoiceCommands;
using System.Reflection.Metadata.Ecma335;

namespace KWire
{
    public interface LevelDetector 
    {
        public event Action<LevelChangedEvent>? OnLevelChanged;
    }
    public class Device : IDisposable, LevelDetector
    {
        // AUDIODEVICE CLASS 
        private int id;
        private string source;
        private string deviceName;
        private int channels;
        private int level;
        private float leveldB;
        private WaveInEvent waveIn; 
        //private readonly ILogger<Device> _logger;

        public event Action<LevelChangedEvent> OnLevelChanged;

        // Constructor enables monitoring of input level by default. 
        public Device(int id, string sourceName, string devName, int channels)
        {
            this.DeviceID = id;
            this.Source = sourceName;
            this.DeviceName = devName;
            this.Channels = channels;
            Logfile.Write("AudioDevice :: Added AudioDevice :  ID :  " + this.DeviceID + " Named: " + this.deviceName + " Input Source: " + this.Source +" . It has " + this.Channels + " channels");

            MonitorLevel();
            Logfile.Write("AudioDevice :: LevelMonitoring enabled for ID : " + this.DeviceID + " AutoCam-source: " + this.Source);
        }

        private string DeviceName
        {
            get { return deviceName; }
            set {deviceName = value;}
        }
        public int DeviceID 
        {
            get { return id; }
            set { id = value;}

        }

        public string Source 
        {
            get { return source; }
            set { source = value; }
        }
        
        public int Channels 
        {
            get { return channels; }
            set { channels = value; }
        }

        public int Level 
        {
            get { return level; }
            private set { level = value; }
        }

        public float LeveldB 
        {
            get { return leveldB; }
            private set { leveldB = value; }
        }

       


        public void MonitorLevel()  
        
        {
           //create a new recording session object
           waveIn = new WaveInEvent();

           //set the recording session device ID
           waveIn.DeviceNumber = id;
           waveIn.DataAvailable += OnDataAvailable;
           waveIn.StartRecording();

            //TODO: this fails when Windows denies access to the microphone due to security settings. Therefore: it needs to check it has permission somehow.. 

        }

        private void OnDataAvailable(object sender, WaveInEventArgs args) //Converts audio level to FloatingPoint 
        {

            float max = 0;
            // interpret as 16 bit audio
            for (int index = 0; index < args.BytesRecorded; index += 2)
            {
                short sample = (short)((args.Buffer[index + 1] << 8) |
                                        args.Buffer[index + 0]);
                // to floating point
                var sample32 = sample / 32768f;
                // absolute value 
                if (sample32 < 0) sample32 = -sample32;
                // is this the max value?
                if (sample32 > max) max = sample32;
                //Console.WriteLine(max);

                level = (int)Math.Floor(max) * 255; // Recalculate level to AutoCam 8-bit integer level.  
                leveldB = max;    // Keep level in dBFS for future use, for debugging in Webconsole. 
                
                if (level > Config.AutoCamLevelTreshold) 
                {
                    var lv = LevelChangedEvent.Create(level);
                    OnLevelChanged?.Invoke(lv);
                }

            }
        
        }

        public void Dispose()
        {
            waveIn.StopRecording();
            waveIn.Dispose();

            Logfile.Write("AudioDevice :: Stopped monitoring of : " + this.DeviceName + " AutoCam-source: " + this.Source);
        }
    }

}

    public class LevelChangedEvent
    {
        public int Level { get; set; }

        public static LevelChangedEvent Create(int level) 
        {
            return new LevelChangedEvent { Level = level };
        }
    }