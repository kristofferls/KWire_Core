using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using Lawo.EmberPlusSharp.S101;
using Lawo.Threading.Tasks;
using Lawo.EmberPlusSharp.Model;

namespace KWire
{
    public static class EmberConsumer
    {


                
    public static void Connect() 
    {
        if (Config.Ember_IP != null || Config.Ember_Port != 0) 
        {
                //Check if the provider is in fact online. 
                bool currentStatus = false; 

                if (Config.Ember_IP == "127.0.0.1") 
                {
                    Console.WriteLine("Emberhost is running locally - will assume DEV MODE - skipping HTTP check");
                    currentStatus = true;
                }
                else 
                {
                    HeartBeat startupCheck = new HeartBeat();

                    currentStatus = startupCheck.PollPowerCore().Result;

                    if (Config.Debug)
                    {
                        Console.WriteLine("EMBERCONSUMER :: Connect :: PowerCoreStatus is " + startupCheck.PowerCoreStatus.ToString());
                    }

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
                            });

                }
                else 
                {
                    Logfile.Write("EMBERCONSUMER :: ERROR :: Provider does not respond to HTTP requests!Terminating");
                    Environment.Exit(1);
                }

                

        }

        else 
        {
                Logfile.Write("EMBERCONSUMER :: ERROR :: No valid IP or Port was found in config. Please check config.xml");    
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
            if(Config.Debug) 
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

                        
                    });
            Logfile.Write("---------------------------EmberPlus tree end ---------------------------");
        }

    }


    public sealed class PowerCoreRoot : DynamicRoot<PowerCoreRoot>
    {
    }



}
