using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using Lawo.EmberPlusSharp.S101;
using Lawo.Threading.Tasks;
using Lawo.EmberPlusSharp.Model;
using System.Net.NetworkInformation;
using System.Diagnostics;

namespace KWire
{
    public static class EmberConsumer
    {

        public static bool IsConnected;
        private static DateTime timeOfError;
        private static bool currentStatus;
        private static CancellationTokenSource cancellationTokenSource;
        private static CancellationToken cancellationToken;
        public static void Connect() 
    {
        if (Config.Ember_IP != null || Config.Ember_Port != 0) 
        {
                //Check if the provider is in fact online. 
                
                    
                    Ping pingTest = new Ping();
                    PingOptions pingOptions = new PingOptions();
                    pingOptions.DontFragment = true;

                    string data = "aaaaaaaaaaaaaaaaaaa";
                    byte[] buffer = Encoding.ASCII.GetBytes(data);
                    int timeOut = 1024;

                    try
                    {
                        PingReply reply = pingTest.Send(Config.Ember_IP, timeOut, buffer, pingOptions);
                    

                    

                    if( reply.Status == IPStatus.Success) 
                    {
                        if (Config.Debug) 
                        {
                            Logfile.Write("EmberConsumer :: Pingtest successfull. Roundtrip time was: " + reply.RoundtripTime);
                        }
                        else
                        {
                            Logfile.Write("EmberConsumer :: Host Provider is available on the network");
                        }
                        
                        currentStatus = true;
                        
                    } 
                    
                    else 
                    
                    {
                        Logfile.Write("EmberConsumer :: FATAL ERROR :: Provider does not respond to ping - can't continue without it");
                        Environment.Exit(0);
                    }


                    }
                    catch (Exception)
                    {
                        throw;
                    }




                if (currentStatus) 
                {
                    // This is necessary so that we can execute async code in a console application.
                    AsyncPump.Run(
                            async () =>
                            {
                            // Establish S101 protocol
                            using (S101Client client = await ConnectAsync(Config.Ember_IP, Config.Ember_Port))

                            // Retrieve *all* elements in the provider database and store them in a local copy
                            using (Consumer<PowerCoreRoot> consumer = await Consumer<PowerCoreRoot>.CreateAsync(client))
                            {
                                // Get the root of the local database.
                                INode root = consumer.Root;
                            }
                            }, cancellationToken);

                }
                else 
                {
                    Logfile.Write("EmberConsumer :: ERROR :: Provider does not respond to HTTP requests!Terminating");
                    
                    if(Config.Debug == false) //Do not actually terminate if in debug mode - just warn. Useful when using TinyEmber for testing. 
                    {
                        Environment.Exit(1);
                    }
                    
                }

                

        }

        else 
        {
                Logfile.Write("EmberConsumer :: ERROR :: No valid IP or Port was found in config. Please check config.xml");    
        }


    }


        public static async Task<S101Client> ConnectAsync(string host, int port)
        {
            try
            {
                // Create TCP connection
                var tcpClient = new TcpClient();
                await tcpClient.ConnectAsync(host, port);

                // Establish S101 protocol
                // S101 provides message packaging, CRC integrity checks and a keep-alive mechanism.
                var stream = tcpClient.GetStream();
                return new S101Client(tcpClient, stream.ReadAsync, stream.WriteAsync);
            }
            catch (Exception err)
            {
                if (Config.Debug)
                {
                    Logfile.Write("EmberConsumer :: ERROR : " + err);

                }


                return null;
            }

        }

        private static void WriteChildren(INode node, int depth)
    {
        var indent = new string(' ', 2 * depth);

        

        foreach (var child in node.Children)
        {
            var childNode = child as INode;

            if (childNode != null)
            {
                    //Console.WriteLine("{0}Node {1}", indent, child.Identifier);
                    Logfile.Write(indent + "Node " + child.Identifier);
                    WriteChildren(childNode, depth + 1);
            }
            else
            {
                var childParameter = child as IParameter;

                if (childParameter != null)
                {
                        //Console.WriteLine("{0}Parameter {1}: {2}", indent, child.Identifier, childParameter.Value);
                        Logfile.Write(indent + "Parameter " + child.Identifier + ": " + childParameter.Value);
                }
            }
        }
    }

    public static void PrintEmberTree() 
    {
       

            Logfile.Write("EmberConsumer :: CURRENT DEVICE TREE");
            Logfile.Write("---------------------------EmberPlus tree begin ---------------------------");

        AsyncPump.Run(
        async () =>
                    {
                        using (var client = await ConnectAsync(Config.Ember_IP, Config.Ember_Port))
                        using (var consumer = await Consumer<PowerCoreRoot>.CreateAsync(client))
                        {
                            WriteChildren(consumer.Root, 0);
                            
                        }

                        
                    }, cancellationToken);

        Logfile.Write("---------------------------EmberPlus tree end ---------------------------");
        
            
        Task.Run(() => MonitorConnection());
            
    }

     

    public static void MonitorConnection() 

            //This method will run in the background, constantly monitoring the connection with the provider. If the connection is lost, it will call cleanup. 
    
    {
       AsyncPump.Run(
        async () =>
        {
            using (var client = await ConnectAsync(Convert.ToString(Config.Ember_IP), Config.Ember_Port))
            using (var consumer = await Consumer<PowerCoreRoot>.CreateAsync(client))
            {
                var connectionLost = new TaskCompletionSource<Exception>();
                //consumer.ConnectionLost += (s, e) => Consumer_ConnectionLost(s, e);
                consumer.ConnectionLost += (s, e) => connectionLost.SetResult(e.Exception);

                Console.WriteLine("Waiting for the provider to disconnect...");
                var exception = await connectionLost.Task;
                
                Console.BackgroundColor = ConsoleColor.Red;
                Console.ForegroundColor = ConsoleColor.White; 
                
                Logfile.Write("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Logfile.Write("EmberConsumer :: !! WARNING !! Connection to provider " + Convert.ToString(Config.Ember_IP) + " was LOST!!");
                Logfile.Write("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Logfile.Write("Exception : " + exception);

                timeOfError = DateTime.Now;

                IsConnected = false;

                
                
            }
        }, cancellationToken);

            ConnectionLostHandler();
        }

    private static void ConnectionLostHandler() 
        {
            //This method is supposed to do : 
            //
            // 1. Remove all EGPI objects. They are essentially not valid any more. 
            
            if (Config.Debug) 
            {
                Console.WriteLine("ConnectionLostHandler");
                Console.WriteLine("EGPIs now " + Core.EGPIs.Count);
                Console.WriteLine("Clearing");
                
                Core.EGPIs.Clear();

                Console.WriteLine("EGPIs now: " + Core.EGPIs.Count);

            }
            else
            {
                Core.EGPIs.Clear();
            }


            // 2. Try to reconnect to the given provider. Continue until it works. Pause for N seconds betwen retries. Do NOT write to log every attempt, but keep track of number of attempts etc. 

            int counter = 0;

            //Console.WriteLine("Conunter is " + counter);

            System.Threading.Thread.Sleep(5000);

            while ( IsConnected == false) 
            {
                try
                {
                    Console.WriteLine("Counter is " + counter);
                    var tcpClient = new TcpClient();
                    tcpClient.Connect(Convert.ToString(Config.Ember_IP), Config.Ember_Port);

                    
                    var stream = tcpClient.GetStream();

                    if (stream == null || tcpClient == null)
                    {
                        //Reconnection failed!
                        if (Config.Debug)
                        {
                            Console.WriteLine("Reconnection attempt nr." + counter);
                        }

                        counter++;
                        System.Threading.Thread.Sleep(5000);
                    }
                    else
                    {
                        if (Config.Debug)
                        {
                            Console.WriteLine("Reconnected <3");
                        }

                        
                        IsConnected = true;
                    }

                    tcpClient.Close();
                    tcpClient.Dispose();
                }
                catch (Exception e)
                {

                    Console.WriteLine(e);
                    continue;
                }


            }


            // 3. When re-established: call Reconnect method. 
            Console.BackgroundColor = ConsoleColor.Black;
            Console.ForegroundColor = ConsoleColor.White;

            var now = DateTime.Now;

            TimeSpan periodOffline = now.Subtract(timeOfError);
                        
            Logfile.Write("EmberConsumer :: ALERT :: Connection re established! It was offline for " + periodOffline.ToString(@"hh\:mm\:ss"));
            Logfile.Write("EmberConsumer :: INFO :: Will wait 30s before attempting reconnection..");

            var stopWatch = Stopwatch.StartNew();
            var timeSpan = new TimeSpan(0, 0, 0, 30); //should be 30s 

            while (stopWatch.Elapsed < timeSpan) 
              {
                if (Config.Debug) 
                {
                    Console.WriteLine("Waited: " + stopWatch.ElapsedMilliseconds / 1000f + " seconds");
                }
                
              }
            
            Logfile.Write("EmberConsumer :: INFO :: Done!");
            
            Reconnect();

        }

    private static void Reconnect() 
        {
            //This method is supposed to do: 
            //
            //  1. Restart MonitorConnection method as a new task. 

            Task.Run(() => MonitorConnection());

            //  2. Re-create all Ember objects from list. 

            Config.ConfigureEGPI();

            Logfile.Write("EmberConsumer :: ALERT :: Reconnection complete!");
            //
            //

        }

    }
    public sealed class PowerCoreRoot : DynamicRoot<PowerCoreRoot>
    {
    }



}
