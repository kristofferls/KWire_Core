using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace KWire
{
    public class WebSocketClient
    {
        //TODO: Make robust!! 

        IPAddress _ip;
        int _port;
        IPEndPoint endPoint;
        Socket socket;
        int _messageCounter;


            /*
        public WebSocketClient(string ip, int port) 
        {
            this._ip = Dns.GetHostAddresses(ip)[0];
            this._port = port; 
        }
        
        */

        public void Disconnect()
        {
            if(socket != null) 
            {
                socket.Close();
                socket.Dispose();
            }     
                        
        }
        public void Connect(IPAddress ip, int port) 
        {
            _ip = ip;
            _port = port;
            _messageCounter = 0;
            endPoint = new IPEndPoint(_ip, _port);

            try 
            {
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                
                //Server must say hello or something for this to work. Disabled for the time beeing. 
                
                /*
                if (PollConnection() == false) 
                {
                    Logfile.Write("WebSocketClient :: NOT CONNECTED! - new attempt in 5 seconds.");
                    Thread.Sleep(5000);
                    Disconnect();
                    Connect(_ip, _port); //Will result in an endless loop unless connected. 
                }
                
                */
            }
            catch (Exception error) 
            {
                Console.WriteLine("WebSocketClient :: {0}", error);
            }
            
            
            
            

        }
        public void Send( byte[] data )
        {
            //Data-type should not be ASCII but something else ?? Float? Should be sent as an array somehow.. 
             
            //byte[] buffer = Encoding.ASCII.GetBytes(data);
            //byte[] buffer = Encoding.

            if (_messageCounter < 100000) 
            {
                try
                {
                    if(socket != null && endPoint != null) 
                    {
                        socket.SendTo(data, endPoint);
                    }
                    
                }
                catch (Exception err)
                {
                    Logfile.Write("WebSocketClient ERROR :: " + err);
                }
            }
                        
            else 
            {
                if (PollConnection() == true) 
                { _messageCounter = 0; }
                else
                {
                    Logfile.Write("WebSocketClient ERROR :: Connection broken - will try to reconnect ");
                    Disconnect();
                    Connect(_ip, _port);
                }
                
            } 
            
            
            
            //System.Console.WriteLine("Sent: " + data);
        }

        public void SendJSON(string message) 
        {
            if(socket != null && endPoint != null) 
            {
                byte[] buffer = Encoding.ASCII.GetBytes(message);

                socket.SendTo(buffer, endPoint);
            } 
                      
        
        }

        private bool PollConnection() 
        {
            
            if (socket != null) 
            {
                bool check1 = socket.Poll(1000, SelectMode.SelectRead);
                bool check2 = (socket.Available == 0);

                if ((check1 && check2) == false)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            return false;    
        }

    }
}
