using Lawo.EmberPlusSharp.Model;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
        private bool _state;
        public string Type { get; set; }
        public int? Id { get; set; }
        public string Name { get; set; }
        public bool State
        {
            get
            {
                return _state;
            }

            set
            {
               
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged();
                }
                
            }

        }


        public EGPI(string name)
        {
            Type = "GPO"; //Hardcoded, to make AutoCam parsing easier. 
            Name = name;
            Id = GetID();
            State = false;
        }

        public EGPI(int id, string name) 
        {
            Type = "GPO";  
            Name = name;
            Id = id;
            State = false;
            HelloWorld();
        }

        public EGPI(int id, string name, bool state)
        {
            Type = "GPO";
            Name = name;
            Id = id;
            State = state;
        }

        public EGPI()
        {
        }

        private void HelloWorld() 
        {
            Logfile.Write("EGPI :: " + Name + " ID:" +  Id.ToString() + " created");
        }

        /// <summary>
        /// Gets triggered if any EGPIs state changes
        /// </summary>

        public event PropertyChangedEventHandler PropertyChanged;

        private int GetID() 
        {
            if (Core.EGPIs != null) 
            {
                if (Core.EGPIs.Count == 0) 
                {
                    return 1;
                }
                else
                {
                    int newID = Core.EGPIs.Count + 1;
                    return newID;
                }
            } 
            else 
            { 
                return 0; 
            }
        }
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) 
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            Logfile.Write("EGPI :: " + this.Name + " state change to " + State.ToString());
        }
           


    }

   
}
