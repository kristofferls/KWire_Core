using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace KWire
{
    public class Response : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e) 
        {
            Console.WriteLine("Received message from client");
            Send(e.Data);
        }

        protected override void OnOpen()
        {
            
        }
    }

    public class SendData : WebSocketBehavior
    {

        

    }
    class WebSocket //WebSocketServer declaration 
    {

        WebSocketServer wss;
        
        private string address; 
        public WebSocket(string ip, int port) 
        {
            this.address = "ws://" + ip + ":" + port;
        }
        public void Start() 
        {
            wss = new WebSocketServer(address); //could be read from config-file. 

            wss.AddWebSocketService<Response>("/Response");

            wss.Start();
            Console.WriteLine("WebSocketServer started with address {0}", address);

            
            
        }

        public void Stop() 
        {
            wss.Stop();
            wss.RemoveWebSocketService(address);
            Console.WriteLine("WebSocketServer stopped on address {0}", address);
        }



    }







}
