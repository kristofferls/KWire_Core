using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Windows.Security.EnterpriseData;

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

        public EventHandler ConnectionLost;
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
                socket.Connect(_ip, _port);
                if (socket.Connected) { Connected = true; }else { Connected = false;}
            }
            catch (Exception error) 
            {
                Console.WriteLine("WebSocketClient :: {0}", error);
            }

        }

        protected virtual void OnConnectionLost(EventArgs e)
        {
            ConnectionLost?.Invoke(this, e);

            Disconnect();
            Console.WriteLine("WebSocketClient :: Reconnection");
            Connect(_ip, _port);
        }
        public async Task SendJSON(string message) 
        {
            
            
            if(socket != null && endPoint != null && socket.Connected) 
            {
                byte[] buffer = Encoding.ASCII.GetBytes(message);

                //socket.SendTo(buffer, endPoint);
                try
                {
                    await socket.SendToAsync(buffer, SocketFlags.None, endPoint);
                }
                catch (Exception e)
                {
                    Logfile.Write("WebSocketClient ERROR:: " + e.Message);
                    Disconnect();
                    Connect(_ip, _port);
                    throw;
                }
                
               
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
