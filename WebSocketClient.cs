using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace KWire
{
    public class WebSocketClient :IDisposable
    {
        //TODO: Make robust!! 

        IPAddress _ip;
        int _port;
        IPEndPoint endPoint;
        Socket socket;
        int _messageCounter;
        private CancellationToken cancellationToken;
        public bool Connected { get; private set; } 

        public void Disconnect()
        {
            if(socket != null) 
            {
                socket.Close();
                socket.Dispose();
                Connected = false;
                cancellationToken = new CancellationToken();
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
                if (socket.Connected) { Connected = true; }else { Connected = false;}
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

        public async Task SendJSON(string message) 
        {
            if(socket != null && endPoint != null && socket.Connected) 
            {
                byte[] buffer = Encoding.ASCII.GetBytes(message);

                //socket.SendTo(buffer, endPoint);
                await socket.SendToAsync(buffer, SocketFlags.None, endPoint);
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

        public void Dispose()
        {
            socket.Dispose();
        }
    }
}
