using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmberPlusConsumerClassLib.EmberHelpers;
using Lawo.EmberPlusSharp.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime;
using KWire;
using KWire_Core.Models.DHD;
using KWire_Core.Models.Lawo;
using System.Runtime.CompilerServices;
using System.Data.Common;
using System.Data;
using Windows.Media.Playback;

namespace KWire_Core
{
    public class EmberConsumerService
    {
        private readonly ILogger<EmberConsumerService> _logger;
        private IEmberPlusConsumer? device = null;
        private string _emberProviderIP;
        private int _emberProviderPort;
        private bool _DHD;


        private ConcurrentDictionary<string, GpioChangedEvent> LogicOutputs { get; set; } = new ConcurrentDictionary<string, GpioChangedEvent>();
        public List<GpioChangedEvent> LogicOutputsList => LogicOutputs.Values.ToList();

        public EmberConsumerService(ILogger<EmberConsumerService> logger, string ProviderIP, int ProviderPort)
        {
            _logger = logger;
            _emberProviderIP = ProviderIP;
            _emberProviderPort = ProviderPort;
            _DHD = Config.DHD;
            logger.LogInformation("EmberConsumer created. IP: " + ProviderIP + " Port: " + ProviderPort);
        }

        private void AddLogicOutput(GpioChangedEvent gpioChangedEvent)
        {
            LogicOutputs.AddOrUpdate(gpioChangedEvent.Identifier, gpioChangedEvent, (key, oldValue) =>
            {
                oldValue.LogicState = gpioChangedEvent.LogicState;
                return oldValue;
            });
        }

        public void ConfigureEmberConsumer()
        {
            if (_DHD)
            {
                // Initiate DHD Consumer
                device = new EmberPlusDhdConsumer(_logger);
                _logger.LogInformation("DHD mode");
            }
            else
            {
                // Initiate Lawo Consumer
                device = new EmberPlusLawoConsumer(_logger);
                _logger.LogInformation("Lawo mode");
            }

            // Setup listener to Ember GPOs
            device.OnLogicOutputChanged += DeviceLogicOut_OnParameterChanged;
        }


        private void DeviceLogicOut_OnParameterChanged(GpioChangedEvent ev)
        {
            _logger.LogInformation($"EmberConsumerService received {ev.Identifier} => {ev.LogicState}");
            AddLogicOutput(ev);
            //_localHubUpdater.LogicOutputList(LogicOutputsList);
        }

    }


    public interface IEmberPlusConsumer
    {
        public event Action<GpioChangedEvent>? OnLogicOutputChanged;
    }

    public class EmberPlusLawoConsumer : IEmberPlusConsumer
    {
        private readonly ILogger<EmberConsumerService> _logger;
        //private readonly AppSettings.KWire _settings;
        private readonly string _ip = Config.Ember_IP;
        private readonly int _port = Config.Ember_Port;
        public DeviceConsumerConnection<PowerCoreRubyRoot> device { get; private set; } = null;

        /// <summary>
        /// Get's the current GPO's outputs state
        /// </summary>
        public ConcurrentDictionary<string, VirtualGeneralPurposeIO> LogicOutputs { get; private set; } = new ConcurrentDictionary<string, VirtualGeneralPurposeIO>();

        /// <summary>
        /// Get's triggered when any of the virtual EmBER+ GPO's are triggered.
        /// </summary>
        public event Action<GpioChangedEvent> OnLogicOutputChanged;

        public EmberPlusLawoConsumer(ILogger<EmberConsumerService> logger)
        {
            _logger = logger;
            

            device = new DeviceConsumerConnection<PowerCoreRubyRoot>(_logger);
            setup(_ip,_port);
        }

        private void setup(string ip, int port)
        {
            device.OnConnectionChanged += Consumer_OnConnectionChanged;
            _ = device.Connect(ip, port);
        }

        private void Consumer_OnConnectionChanged(string arg1, bool connected)
        {
            _logger.LogInformation($"Connection changed: {arg1} {connected}");

            if (connected)
            {
                Task.Run(async () => {
                    await Task.Delay(2000);

                    while (!device.Consumer.Root.IsOnline)
                    {
                        await Task.Delay(2000);
                        _logger.LogInformation("Waiting for Root");
                    }

                    INode inputs = await device.Consumer.Root.NavigateToNode<PowerCoreRubyRoot>($"Ruby/GPIOs/{Config.Ember_ProviderName}/Output Signals", device.Consumer);
                    if (inputs != null)
                    {
                        var all = await inputs.ChildNodes(device.Consumer);
                        foreach (var nod in all)
                        {
                            _logger.LogInformation($" - Listen to ${nod.Identifier}");
                            IParameter stateParameter = await nod.GetParameter("State", device.Consumer);

                            if (stateParameter != null)
                            {
                                // Subscribe to changes
                                stateParameter.PropertyChanged += LogicOutputStateParameter_PropertyChanged;

                                // Send out current state information to listeners
                                var ev = GpioChangedEvent.Create(stateParameter.Parent.Identifier, (bool)stateParameter.Value);
                                OnLogicOutputChanged?.Invoke(ev);

                                // Add the tree-node to the list to be able to unsubscribe to PropertyChanged events
                                VirtualGeneralPurposeIO logicOut = new VirtualGeneralPurposeIO()
                                {
                                    Name = stateParameter.Parent.Identifier,
                                    TreeParameter = stateParameter,
                                    IsActive = ev.LogicState,
                                };
                                LogicOutputs.GetOrAdd(stateParameter.Parent.Identifier, logicOut);
                            }
                        }
                    }
                });
            }
        }

        private void LogicOutputStateParameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender != null)
            {
                // Trigger event on change
                var data = (IParameter)sender;
                var ev = GpioChangedEvent.Create(data.Parent.Identifier, (bool)data.Value);
                OnLogicOutputChanged?.Invoke(ev);

                // Update post in the list
                LogicOutputs.AddOrUpdate(data.Parent.Identifier, new VirtualGeneralPurposeIO()
                {
                    Name = data.Parent.Identifier,
                    TreeParameter = data,
                    IsActive = ev.LogicState,
                }, (key, oldValue) =>
                {
                    oldValue.IsActive = ev.LogicState;
                    return oldValue;
                });

                _logger.LogInformation($"{data.Parent.Identifier} changed to {(bool)data.Value}");
            }
        }
    }

    public class GpioChangedEvent
    {
        public string Identifier { get; set; }
        public bool LogicState { get; set; }

        public static GpioChangedEvent Create(string identifier, bool state)
        {
            return new GpioChangedEvent()
            {
                Identifier = identifier,
                LogicState = state

            };

        }

    }

    public class EmberPlusDhdConsumer : IEmberPlusConsumer
    {
        private readonly ILogger<EmberConsumerService> _logger;
        private readonly string _ip;
        private readonly int _port;

        public DeviceConsumerConnection<DHD52Root> device { get; private set; } = null;

        /// <summary>
        /// Get's the current GPO's outputs state
        /// </summary>
        public ConcurrentDictionary<string, VirtualGeneralPurposeIO> LogicOutputs { get; private set; } = new ConcurrentDictionary<string, VirtualGeneralPurposeIO>();

        /// <summary>
        /// Get's triggered when any of the virtual EmBER+ GPO's are triggered.
        /// </summary>
        public event Action<GpioChangedEvent>? OnLogicOutputChanged;

        public EmberPlusDhdConsumer(ILogger<EmberConsumerService> logger)
        {
            _logger = logger;
            _ip = Config.Ember_IP;
            _port = Config.Ember_Port;

            device = new DeviceConsumerConnection<DHD52Root>(_logger);
            setup(_ip, _port);
        }

        private void setup(string ip, int port)
        {
            device.OnConnectionChanged += Consumer_OnConnectionChanged;
            _ = device.Connect(ip, port);
        }

        private void Consumer_OnConnectionChanged(string arg1, bool connected)
        {
            _logger.LogInformation($"Connection changed: {arg1} {connected}");

            if (connected)
            {
                Task.Run(async () => {
                    _logger.LogInformation($"Waiting a bit..");
                    await Task.Delay(2000);                    

                    while (!device.Consumer.Root.IsOnline)
                    {
                        await Task.Delay(2000);
                        _logger.LogInformation("Waiting for Root");
                    }

                    INode inputs = await device.Consumer.Root.NavigateToNode<DHD52Root>($"Device/GPO", device.Consumer);
                    if (inputs != null)
                    {
                        var all = await inputs.ChildParameterNodes(device.Consumer);
                        foreach (var stateParameter in all)
                        {
                            _logger.LogInformation($" - Listen to ${stateParameter.Description}");

                            if (stateParameter != null)
                            {
                                // Subscribe to changes
                                stateParameter.PropertyChanged += LogicOutputStateParameter_PropertyChanged;

                                // Send out current state information to listeners
                                var ev = GpioChangedEvent.Create(stateParameter.Description, (bool)stateParameter.Value);
                                OnLogicOutputChanged?.Invoke(ev);

                                // Add the tree-node to the list to be able to unsubscribe to PropertyChanged events
                                VirtualGeneralPurposeIO logicOut = new VirtualGeneralPurposeIO()
                                {
                                    Name = stateParameter.Description,
                                    TreeParameter = stateParameter,
                                    IsActive = ev.LogicState,
                                };
                                LogicOutputs.GetOrAdd(stateParameter.Description, logicOut);

                                //Check if changed EGPIO is one listed as a logic of interest in List EGPI in Core: 
                                UpdateEGPIList(ev);
                            }
                        }
                    }
                });
            }
        }

        private void UpdateEGPIList(GpioChangedEvent ev) 
        {
            if (Core.EGPIs != null) 
            {
                //try get the ID
                EGPI _egpi;
                
                
                bool idexists = Core.EGPIs.TryGetValue(ev.Identifier, out _egpi);


                if (idexists && _egpi.Id != null)  
                {
                    _logger.LogInformation("Got a match in Core.EGPIs dicitonary : " + ev.Identifier + " == " + _egpi.Name);
                    _logger.LogInformation("State was: " + _egpi.State.ToString() + " New state is: " + ev.LogicState.ToString());

                    Core.EGPIs.AddOrUpdate(ev.Identifier, new EGPI()
                    {
                        Name = ev.Identifier,
                        Id = _egpi.Id,
                        State = ev.LogicState
                    }, (key, oldValue) => 
                    { 
                        if(oldValue.State != ev.LogicState) 
                        {
                            oldValue.State = ev.LogicState;
                        }
                        return oldValue;
                    });;
                }

            }
            
        
        }

        private void LogicOutputStateParameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender != null)
            {
                // Trigger event on change
                var data = (IParameter)sender;
                var ev = GpioChangedEvent.Create(data.Description, (bool)data.Value);
                OnLogicOutputChanged?.Invoke(ev);

                // Update post in the list
                LogicOutputs.AddOrUpdate(data.Parent.Description, new VirtualGeneralPurposeIO()
                {
                    Name = data.Parent.Description,
                    TreeParameter = data,
                    IsActive = ev.LogicState,

                }, (key, oldValue) =>
                {
                    oldValue.IsActive = ev.LogicState;
                    return oldValue;
                });;

                UpdateEGPIList(ev);

                _logger.LogInformation($"{data.Description} changed to {(bool)data.Value}");
            }
        }
    }


}
