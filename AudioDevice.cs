using NAudio.Wave;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KWire
{
    public class Device
    {
        // AUDIODEVICE CLASS 
        private int id;
        private string source;
        private string deviceName;
        private int channels;
        private float level;

        //

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

        public float Level 
        {
        
            get { return level; }
            private set { level = value; }
        }
        
 
      
        

        public void MonitorLevel()  
        
        {
           //create a new recording session object
           var waveIn = new WaveInEvent();

           //set the recording session device ID
           waveIn.DeviceNumber = id;
           waveIn.DataAvailable += OnDataAvailable;
           waveIn.StartRecording();

           
           
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

                level = max; 
            }
        }





    }



}
