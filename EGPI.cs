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
        public event PropertyChangedEventHandler PropertyChanged;

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
                    OnStateChanged();
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

        protected virtual void OnStateChanged([CallerMemberName] string propertyName = null) 
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

            Logfile.Write("EGPI :: " + this.Name + " state change to " + State.ToString());
        }

    }


}
