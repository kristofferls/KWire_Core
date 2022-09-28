using Lawo.EmberPlusSharp.Model;
using Lawo.Threading.Tasks;
using System.Diagnostics;

namespace KWire
{
    public class EGPI
    {

        private int _id;
        private string _name;
        private bool _state;
        private bool _stateChanged;
        private Stopwatch _lastChange;
        public DateTime? LastChange;
        private DateTime _timeOfError;
        public bool _disconnected;
        private Task _monitor; 
        

        public EGPI(int id, string name)
        {
            _id = id;
            _name = name;
            _disconnected = false;
            _monitor = null;
            Logfile.Write("EGPI :: EGPI with ID: " + _id + " and name: " + _name + " created");
            GetState();
            //string currState = currentState.ToString();

            

            Logfile.Write("EGPI :: Current state of " + this._id + ":" + this._name + " is:: " + this._state);
            Logfile.Write("EGPI :: Enabling async monitoring of " + this._id + ":" + this._name);
            //Task.Run(() => TaskMonitor());
            Task.Run(()=> MonitorState());


        }

        public int GPO
        {
            get { return _id; }
            set { _id = value; }

        }

        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        public bool Status
        {
            get { return _state; }
            private set { _state = value; }
        }

        public void GetState()
        {
           GetCurrentState();
            if (_state == true)
            {
                Logfile.Write("EGPI :: " + this._name + " is ON / TRUE");
                
            }
            else if (_state == false)
            {
                Logfile.Write("EGPI :: " + this._name + " is OFF / FALSE");
                
            }
        }

        private async void TaskMonitor() //NOT IMPLEMENTED 
        {
            var taskFactories = new List<Func<Task>>();
            taskFactories.Add(() => WaitForChange());

            var runningTasks = taskFactories.ToDictionary(factory => factory());

            while (runningTasks.Count > 0) 
            {
                var completedTasks = await Task.WhenAny(runningTasks.Keys);
                if (completedTasks.IsFaulted) 
                {
                    Logfile.Write("EGPI " + Convert.ToString(this._name) + " :: Provider connection lost!");
                    var factory = runningTasks[completedTasks];
                    var newTask = factory();
                    runningTasks.Add(newTask, factory);
                }
                if (completedTasks.IsCanceled) 
                {
                    Console.WriteLine("Task completed - creating a new one");
                    var factory = runningTasks[completedTasks];
                    var newTask = factory();
                    runningTasks.Add(newTask, factory);
                }
                else
                {
                    runningTasks.Remove(completedTasks);
                    taskFactories.Add(()=> WaitForChange());
                }
                
            }

        }
        public async Task MonitorState()
        {
            if (_monitor != null) 
            {
                if (_monitor.IsCompleted == false)
                {
                    Logfile.Write("EGPI :: Monitor state called before old task completed! This should NOT occur!");
                }
            }
            
            else 
            {
                _monitor = WaitForChange();
                await _monitor.ConfigureAwait(false);
                if (Config.Debug && _monitor.IsCompleted == true) 
                {
                    Console.WriteLine("EGPI :: " + this._name + " async task completed successfully");
                }
                Reload();
            }
            
           
                        
        }

        private void Reload() 
        {
            if (this._disconnected == true) 
            {
                
            }
            _monitor.Dispose();
            _monitor = null;

            Task.Run(() => MonitorState());
            if (Config.Debug == true) 
            {
                Console.WriteLine("EGPI :: " + this._name + " monitor task completed - restarting");
            }
        }

        private async Task WaitForChange ()
        {
           

            await Task.Run(() => 
            {
                //var valueChanged = new TaskCompletionSource<string>();
                try 
            {
               AsyncPump.Run(
               async () =>
               {

                   using (var client = await EmberConsumer.ConnectAsync(Config.Ember_IP, Config.Ember_Port))
                   using (var consumer = await Consumer<PowerCoreRoot>.CreateAsync(client))
                   {
                       INode root = consumer.Root;

                       if (this._disconnected == true && EmberConsumer.IsConnected == true) //DOES NOT WORK AS EXPECTED!!  

                       {

                           DateTime now = new DateTime();
                           now = DateTime.Now;

                           TimeSpan periodOffline = now.Subtract(_timeOfError);

                           Logfile.Write("EGPI :: WARNING :: " + this._name + " is online again! It was offline for " + periodOffline.ToString(@"hh\:mm\:ss"));
                           this._disconnected = false;
                       }


                        
                       if(Config.DHD) //Sept 2022: Added support for DHD console - it has a different tree structure to Lawo. A LOOOOT simpler. 
                       {
                           var mixer = (INode)root.Children.First(c => c.Identifier == "Device"); //Defined by Lawo / OnAirDesigner
                           var gpios = (INode)mixer.Children.First(c => c.Identifier == "GPO"); //Defined by Lawo / OnAirDesigner
                           var egpio_autocam = gpios.Children.First(c => c.Description == _name);//Config.Ember_ProviderName); //Set in OnAirDesigner, and is red from setting.xml.
                            

                           var valueChanged = new TaskCompletionSource<string>();



                           //Raise an event if the value changes. 
                           egpio_autocam.PropertyChanged += (s, e) => valueChanged.SetResult(((IElement)s).GetPath()); //Tell API that we are interested in this value if it changes. 

                           Logfile.Write("EGPI :: ID: " + this._id + " NAME: " + this._name + " with path " + await valueChanged.Task + " has changed.");

                           // We know that the state have changed, but to what? Read the state, and store it in memory.

                           var stateParameter = egpio_autocam as IParameter;
                           _state = Convert.ToBoolean(stateParameter.Value);

                           if (_state == true)
                           {
                               Logfile.Write("EGPI :: " + this._name + " is ON / TRUE");

                               this.LastChange = DateTime.Now;
                           }
                           else if (_state == false)
                           {
                               Logfile.Write("EGPI :: " + this._name + " is OFF / FALSE");

                           }


                       }

                       else //Traditional Lawo
                       {
                           var mixer = (INode)root.Children.First(c => c.Identifier == "Ruby"); //Defined by Lawo / OnAirDesigner
                           var gpios = (INode)mixer.Children.First(c => c.Identifier == "GPIOs"); //Defined by Lawo / OnAirDesigner
                           var egpio_autocam = (INode)gpios.Children.First(c => c.Identifier == Config.Ember_ProviderName); //Set in OnAirDesigner, and is red from setting.xml.
                           var output_signals = (INode)egpio_autocam.Children.First(c => c.Identifier == "Output Signals"); //This name is hard coded from Lawo.
                           var gpo = (INode)output_signals.Children.First(c => c.Identifier == this._name); //Comes from settings.xml, and needs to correspond EXACTLY with what is defined in OnAirDesigner. 
                           var state = gpo.Children.First(c => c.Identifier == "State");//Hardcoded from Lawo. 

                           var valueChanged = new TaskCompletionSource<string>();



                           //Raise an event if the value changes. 
                           state.PropertyChanged += (s, e) => valueChanged.SetResult(((IElement)s).GetPath()); //Tell API that we are interested in this value if it changes. 

                           Logfile.Write("EGPI :: ID: " + this._id + " NAME: " + this._name + " with path " + await valueChanged.Task + " has changed.");

                           // We know that the state have changed, but to what? Read the state, and store it in memory.

                           var stateParameter = state as IParameter;
                           _state = Convert.ToBoolean(stateParameter.Value);

                           if (_state == true)
                           {
                               Logfile.Write("EGPI :: " + this._name + " is ON / TRUE");

                               this.LastChange = DateTime.Now;
                           }
                           else if (_state == false)
                           {
                               Logfile.Write("EGPI :: " + this._name + " is OFF / FALSE");

                           }

                       }
                       
                       
                       /*
                       //Should cancel the task if the connection is lost. 
                       var connectionLost = new TaskCompletionSource<Exception>();

                       consumer.ConnectionLost += (s, e) => connectionLost.SetResult(e.Exception);
                       Console.WriteLine("EGPI " + this._name + " Connection lost!", await connectionLost.Task);
                       */
                   }
               });
                
            }
            catch(Exception error) 
            {
                if (this._disconnected == false) 
                {
                    Logfile.Write("EGPI :: ID: " + this._id + " NAME: " + this._name + " ERROR :: " + error.ToString());
                    Logfile.Write("EGPI :: ID: " + this._id + " NAME: " + this._name + " Supressing futher errors until resolved!");
                    
                    this._timeOfError = DateTime.Now;                        
                }
                    this._disconnected = true;             
                //throw;                
            }
            
            });
            

        }

        private void Consumer_ConnectionLost(object sender, Lawo.IO.ConnectionLostEventArgs e)
        {
            throw new NotImplementedException();
        }

        public void GetCurrentState() 
        {
            //This method is somehwat deprecated, as it is only used once. WaitForChange does the same thing, although waits for state to change. 

            if (Config.DHD) 
            {
                Logfile.Write("EGPI :: INFO :: DHD mode is set. This function is currently not implemented for DHD");
            }
            else
            {
                try
                {
                    AsyncPump.Run(
                   async () =>
                   {
                       using (var client = await EmberConsumer.ConnectAsync(Config.Ember_IP, Config.Ember_Port))
                       using (var consumer = await Consumer<PowerCoreRoot>.CreateAsync(client))
                       {
                           INode root = consumer.Root;

                       // Navigate to the parameter we're interested in.

                           var mixer = (INode)root.Children.First(c => c.Identifier == "Ruby"); //Defined by Lawo / OnAirDesigner
                           var gpios = (INode)mixer.Children.First(c => c.Identifier == "GPIOs"); //Defined by Lawo / OnAirDesigner
                           var egpio_autocam = (INode)gpios.Children.First(c => c.Identifier == Config.Ember_ProviderName); //Set in OnAirDesigner, and is red from setting.xml.
                           var output_signals = (INode)egpio_autocam.Children.First(c => c.Identifier == "Output Signals");
                           var gpo = (INode)output_signals.Children.First(c => c.Identifier == this._name); //Comes from settings.xml, and needs to correspond EXACTLY with what is defined in OnAirDesigner. 
                           var state = gpo.Children.First(c => c.Identifier == "State");

                       //Read current state of variable. The return is cast as a string, so in order to use it elsewhere, it's cast to bool by Convert.ToBoolean. 
                           var stateParameter = state as IParameter;
                           _state = Convert.ToBoolean(stateParameter.Value);


                       }
                   });
                }
                catch (Exception err)

                {
                    if (Config.Debug)
                    {
                        Logfile.Write("EGPI :: GetCurrentState ERROR :: " + err);
                    }
                    else
                    {
                        Logfile.Write("EGPI :: GetCurrentState ERROR :: Ember provider offline?");
                    }

                }
            }

            
           
        }
        
       













    }




}
