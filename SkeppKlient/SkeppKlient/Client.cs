using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace SkeppKlient
{
    class Client
    {
        // allt är private för att de inte används utanför denna klass
        readonly int port = 8080;
        static bool allIPsTried = false;
        static string localIP1 = "192.168.1";
        static string localIP2 = "10.151.";
        const int bufferSize = 1024; // max data per meddelande i bytes // const för annars squiggly
        static readonly byte[] buffer = new byte[bufferSize];
        static bool isConnected = false; // blir true när man anslutit
        static Socket serverSocket; // deklareras här för tillgång senare, måste vara static

        public Client()
        {
            CheckConnections(CleanArp(GetArp()));
        }

        private static List<string> CleanArp(string arp)
        {
            List<string> listOfIPs = new List<string>();
            string ipRange = @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b";

            // Create a Regex  
            Regex regex = new Regex(ipRange);
            // Get all matches  
            MatchCollection matchedIPs = regex.Matches(arp);
            // Print all matched IPs
            for (int i = 0; i < matchedIPs.Count; i++)
            {
                listOfIPs.Add(matchedIPs[i].Value);
            }
            for (int i = 68; i < 73; i++)
            {
                for (int j = 0; j < 255; j++)
                {
                    //string ip1Copy = string.Format(localIP1 + "{0}.{1}", i, j);
                    string ip2Copy = string.Format(localIP2 + "{0}.{1}", i, j);
                    //Console.WriteLine(ip1Copy);
                    Console.WriteLine(ip2Copy);
                    if (listOfIPs.Contains(ip2Copy))
                    {
                    }
                    else listOfIPs.Add(ip2Copy);
                }
            }

            return listOfIPs;
        }

        private static string GetArp()
        {
            Process process = new Process();
            process.StartInfo.FileName = "cmd.exe";
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.Start();
            process.StandardInput.WriteLine("arp -a");
            process.StandardInput.Flush();
            process.StandardInput.Close();
            process.WaitForExit();
            //Console.WriteLine(process.StandardOutput.ReadToEnd());
            return process.StandardOutput.ReadToEnd().ToString();
        }

        private void CheckConnections(List<string> ipList)
        {
            Console.WriteLine("Please wait while a server is found, this can take a moment");
            int progressBar = 0;

            for (int i = 0; i < ipList.Count; i++)
            {
                Console.SetCursorPosition(0, 2);
                Console.WriteLine("Trying to connect to server {0}/{1}...\n", (i + 1), ipList.Count);
                try
                {
                    TestConnect(ipList[i], port);

                    if (isConnected)
                    {
                        break;
                    }
                }
                catch (SocketException ex)
                {
                    // något gick fel
                }
            }

            if (!isConnected)
            {
                Console.WriteLine("No game servers found");
            }
        }

        private static void ListenForData(IAsyncResult AR) // körs i bakgrunden 
        {
            Socket current = (Socket)AR.AsyncState; // samma klient som innan
            int received; // mängden bytes som tas emot

            try
            {
                received = current.EndReceive(AR); // data från servern
            }
            catch (SocketException) // ingen endreceive betyder ingen klient
            {
                Console.WriteLine("Lost connection to server");
                current.Close(); // stänger ner kopplingen mellan server och klient så att klienten kan koppla upp igen efter omstart
                //clients.Remove(current); // tar bort den nuvarande socketen om den in
                return; // om socketexception skippas resten av metoden
            }

            byte[] receiveBuffer = new byte[received]; // bytearray som lika lång som den in
            Array.Copy(buffer, receiveBuffer, received); // kopierar buffer till receiveBuffer index 0 till received
            string message = Encoding.UTF8.GetString(receiveBuffer); // gör konverterar receivebuffer från bytes till UTF8 och lägger i en string

            if (message == "Welcome to battleship!")
            {
                isConnected = true;
                Console.Clear();
                Console.WriteLine("Connected to server");
            }

            //PrepareResponse();

            current.BeginReceive(buffer, 0, bufferSize, SocketFlags.None, ListenForData, current); // kör metoden igen
        }

        private static void ReadResponse(IAsyncResult AR)
        {
            while (serverSocket.Connected) // så länge klienten är uppkopplad till servern
            {
                try
                {
                    var buffer = new byte[1024]; // max bytes per svar
                    int received = serverSocket.Receive(buffer, SocketFlags.None); // antalet bytes i svaret
                    if (received == 0) return; // svaret var 0 långt
                    var data = new byte[received];
                    Array.Copy(buffer, data, received);
                    string response = Encoding.UTF8.GetString(data); // gör bytes till text
                }
                catch (Exception ex) // servern har stängts ner
                {
                    Console.WriteLine(ex.Message.ToString());
                    Environment.Exit(0); // crashar inte, jag stänger ner den för att inte krasha ok?
                }
            }
        }

        private static void CloseAllSockets(Socket socket) // stänger ner uppkopplingen till alla klienter
        {
            // stänger ner alla klienters sockets
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
        }

        private void TestConnect(string ip, int port)
        {
            Socket current = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            serverSocket = current;
            // Connect using a timeout (5 seconds)

            IAsyncResult result = serverSocket.BeginConnect(ip, port, null, null);

            bool success = result.AsyncWaitHandle.WaitOne(40, true);


            if (serverSocket.Connected)
            {
                serverSocket.EndConnect(result);
                serverSocket.BeginReceive(buffer, 0, bufferSize, SocketFlags.None, ListenForData, serverSocket);
            }
            else
            {
                // NOTE, MUST CLOSE THE SOCKET

                serverSocket.Close();
                //throw new ApplicationException("Failed to connect server.");
            }
        }
    }
}
