using Lawo.EmberPlusSharp.Model;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KWire
{
    public class VirtualGeneralPurposeIO
    {
        public string Name { get; set; }
        public IParameter TreeParameter { get; set; }
        public bool IsActive { get; set; }

    }

    public class EGPI : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private ILogger<EGPI> _logger;

        private bool _state { get; set; }
        public string Type { get; set; }
        public int? Id { get; set; }
        public string Name { get; set; }
        [JsonIgnore]
        public string? URLOn { get; set; }
        [JsonIgnore]
        public string? URLOff { get; set; }
        public bool? State
        {
            get
            {
                return _state;
            }

            set
            {
                
                if (_state != value)
                {
                    if (value != null)
                    {
                        _state = (bool)value;
                        //Logfile.Write("EGPI :: " + this.Name + " state change to " + State.ToString() + " Changed " + _statechangecounter.ToString());
                        
                        //OnStateChanged();
                    }
                }
                
            }

        }

        /*
        public EGPI(string name)
        {
            Type = "GPO"; //Hardcoded, to make AutoCam parsing easier. 
            Name = name;
            Id = GetID();
            State = false;
        }
        */
        public EGPI(int id, string name) 
        {
            Type = "GPO";  
            Name = name;
            Id = id;
            State = null;
            HelloWorld();
        }
        public EGPI(int id, string name, bool state)
        {
            Type = "GPO";
            Name = name;
            Id = id;
            State = state; 
            
        }

        public EGPI(int id, string name, string urlON, string urlOFF, ILogger<EGPI> logger) 
        {
            ///Create a EGPI Object with URL/API action to be triggered when state changes. 
            Type = "GPO";
            Name = name;
            Id = id;
            URLOff = urlOFF;
            URLOn = urlON;
            _logger = logger;
        
        }

        public EGPI()
        {
        }

        private void HelloWorld() 
        {
            Logfile.Write("EGPI :: " + Name + " ID:" +  Id.ToString() + " created");
        }

        protected virtual void OnStateChanged([CallerMemberName] string propertyName = null) 
        {
            //PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            Logfile.Write("EGPI :: " + this.Name + " state change to " + State.ToString());

            if (State == false && URLOff != null) 
            {
                Task.Run(async () => { await ProcessURL(URLOff); });
            }

            if (State == true && URLOn != null)
            {
                Task.Run(async () => { await ProcessURL(URLOff); });
            }
        }

        private async Task ProcessURL(string url) 
        {
            using HttpClient client = new HttpClient();
            
            {
                client.DefaultRequestHeaders.Accept.Clear();

                try
                {
                    var response = await client.GetAsync(url);
                    _logger.LogInformation(this.Name + " Got response: " +  response.StatusCode);

                    if (response.IsSuccessStatusCode) 
                    {
                        _logger.LogInformation(this.Name + " Successfully sent HTTP request");
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message, e);
                    Logfile.Write("EGPI " + this.Name + " ERROR:: " + e.StackTrace);
                }
 
            }
            

        }

    }


}
