using System;
using System.Collections.Generic;
using System.Text;
using System.Net; // behövs för att kommunicera över nätverk
using System.Net.Sockets; // denna med
using System.IO;
using System.Threading.Tasks;

namespace SänkaSkepp
{
    class Server
    {
        // readonly för att inte kunna modifieras senare
        readonly static Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); // deklareras för användning i alla klassens metoder om det behövs
        static List<Socket> clients = new List<Socket>(); // list som alla unika ip addresser och portar förvaras. det är en list för att den kommer öka med tiden
        private const int bufferSize = 2048; // max data per meddelande i bytes // const för annars squiggly
        private readonly int port = 8080; // porten som servern lyssnar på
        private static readonly byte[] buffer = new byte[bufferSize];

        public Server()
        {
            StartServer(); // startar servern
            Console.WriteLine("Press [Enter] to close the server");
            Console.ReadLine(); // klickar man enter i servern stängs den ner
            CloseAllSockets(); // stänger ner alla sockets innan server stänger ner
        }

        private void StartServer()
        {
            Console.WriteLine("Setting up server...");
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, port)); // serverns socket binder till den lokala addressen
            serverSocket.Listen(12); // antalet anslutnings requests det får finnas utan en förlägning, inte min Listen
            serverSocket.BeginAccept(AcceptClient, null); // async metod, körs tills servern stänger ner
            Console.WriteLine("Server " + GetLocalIPAddress() + " is online... \n");
        }

        private static void AcceptClient(IAsyncResult AR) // kör i bakgrunden och ansluter till klienten
        {
            Socket socket; // klientsocket

            try
            {
                // metoden hänger upp sig här i väntan på en client
                socket = serverSocket.EndAccept(AR); // socketen godkänner uppkopplingen
            }
            catch (ObjectDisposedException) // I cannot seem to avoid this (on exit when properly closing sockets)
            {
                return;
            }

            clients.Add(socket); // lägger clienten i en lista för användning senare
            socket.BeginReceive(buffer, 0, bufferSize, SocketFlags.None, ListenForData, socket); // när anslutningen är godkänd börjar servern att lyssna
            Console.WriteLine("Client connected");

            byte[] data = Encoding.UTF8.GetBytes("Welcome to battleship!");
            Send(data);

            serverSocket.BeginAccept(AcceptClient, null); // kör denna metod igen 
            // kan bara vänta på en client i taget
        }

        private static void ListenForData(IAsyncResult AR) // körs i bakgrunden 
        {
            Socket current = (Socket)AR.AsyncState; // samma klient som innan
            int received; // mängden bytes som tas emot

            try
            {
                received = current.EndReceive(AR); // data från clienten
            }
            catch (SocketException) // ingen endreceive betyder ingen klient
            {
                Console.WriteLine("Client disconnected");
                current.Close(); // stänger ner kopplingen mellan server och klient så att klienten kan koppla upp igen efter omstart
                clients.Remove(current); // tar bort den nuvarande socketen om den in
                return; // om socketexception skippas resten av metoden
            }

            byte[] receiveBuffer = new byte[received]; // bytearray som lika lång som den in
            Array.Copy(buffer, receiveBuffer, received); // kopierar buffer till receiveBuffer index 0 till received
            string message = Encoding.UTF8.GetString(receiveBuffer); // gör konverterar receivebuffer från bytes till UTF8 och lägger i en string
            Console.WriteLine("Received Text: " + message); // rå data från klienten innan manipulering

            current.BeginReceive(buffer, 0, bufferSize, SocketFlags.None, ListenForData, current); // kör metoden igen
        }

        private static void Send(byte[] data)
        {
            try
            {
                for (int i = 0; i < clients.Count; i++) // för varje klient
                {
                    clients[i].Send(data); // skicka datan till klient nummer i i klientlistan tills alla klienter har fått ett svar
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString() + " Sending failed");
            }
            Console.WriteLine("Data sent to clients");
        }

        private static void CloseAllSockets() // stänger ner uppkopplingen till alla klienter
        {
            foreach (Socket socket in clients)
            {
                // stänger ner alla klienters sockets
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
            }

            // stänger till sist ner serverns socket
            serverSocket.Close();
        }

        private string GetLocalIPAddress() // returnerar den lokala ipaddressen för servern
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList) // går igenom alla ipaddresser i det lokala nätverket
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!"); // om inte ipadressen hittades
        }

        /*private void BroadCast()
        {
            UdpClient udpClient = new UdpClient();
            udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, UDPPort));

            var from = new IPEndPoint(0, 0);
            Task.Run(() =>
            {
                while (true)
                {
                    var recvBuffer = udpClient.Receive(ref from);
                    Console.WriteLine(Encoding.UTF8.GetString(recvBuffer));
                }
            });

            var data = Encoding.UTF8.GetBytes("ABCD");
            udpClient.Send(data, data.Length, "255.255.255.255", UDPPort);
        }*/
    }
}
