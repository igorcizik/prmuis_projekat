using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client
{
    internal class Client
    {
        static void Main(string[] args)
        {
            Socket clientSocket = null;

            Socket udpSock = null;
            EndPoint serverUdpEP = null;
            bool usingUdp = false;

            try
            {
                clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, 9000);

                Console.WriteLine("Klijent je spreman za povezivanje sa serverom. Pritisni ENTER...");
                Console.ReadLine();

                clientSocket.Connect(serverEP);
                Console.WriteLine("Klijent je uspesno povezan sa serverom!");

                while (true)
                {
                    string serverLine;

                    if (!usingUdp)
                    {
                        serverLine = ReceiveLineTcp(clientSocket);
                    }
                    else
                    {
                        serverLine = ReceiveLineUdp(udpSock, ref serverUdpEP);
                    }

                    if (serverLine == null)
                    {
                        Console.WriteLine("Server je zatvorio konekciju.");
                        Console.WriteLine("Pritisni taster za izlaz...");
                        Console.ReadKey();
                        return;
                    }

                    Console.WriteLine(serverLine);

                    
                    if (!usingUdp && serverLine.StartsWith("ERR", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Login neuspesan. Klijent zavrsava.");
                        Console.WriteLine("Pritisni taster za izlaz...");
                        Console.ReadKey();
                        return;
                    }

                    
                    if (!usingUdp && serverLine.Trim().Equals("Username:", StringComparison.OrdinalIgnoreCase))
                    {
                        string username = Console.ReadLine();
                        SendLineTcp(clientSocket, username);
                        continue;
                    }
                    else if (!usingUdp && serverLine.Trim().Equals("Sifra:", StringComparison.OrdinalIgnoreCase))
                    {
                        string pass = Console.ReadLine();
                        SendLineTcp(clientSocket, pass);
                        continue;
                    }

                    
                    if (!usingUdp && serverLine.StartsWith("OK", StringComparison.OrdinalIgnoreCase) && serverLine.Contains("UDPPORT"))
                    {
                        if (!TryParseUdpPort(serverLine, out int udpPort))
                        {
                            Console.WriteLine("Greska: ne mogu da procitam UDPPORT iz poruke: " + serverLine);
                            return;
                        }

                        
                        udpSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        udpSock.Bind(new IPEndPoint(IPAddress.Any, 0)); 

                        serverUdpEP = new IPEndPoint(((IPEndPoint)serverEP).Address, udpPort);

                        
                        SendLineUdp(udpSock, serverUdpEP, "HELLO");

                        usingUdp = true;
                        continue;
                    }

                    
                    if (serverLine.Trim().Equals("Dostupno slanje komandi (unesi kraj za izlaz)", StringComparison.OrdinalIgnoreCase))
                    {
                        string komanda = Console.ReadLine();

                        if (!usingUdp)
                            SendLineTcp(clientSocket, komanda);
                        else
                            SendLineUdp(udpSock, serverUdpEP, komanda);
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Socket greska: " + ex.Message);
                Console.WriteLine("Pritisni taster za izlaz...");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greska: " + ex);
                Console.WriteLine("Pritisni taster za izlaz...");
                Console.ReadKey();
            }
        }

        private static void SendLineTcp(Socket socket, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\r\n");
            socket.Send(data);
        }

        private static string ReceiveLineTcp(Socket socket)
        {
            StringBuilder sb = new StringBuilder();
            byte[] buffer = new byte[1];

            while (true)
            {
                int bytesRead = socket.Receive(buffer);
                if (bytesRead == 0)
                    return null;

                char c = (char)buffer[0];

                if (c == '\n')
                    break;

                if (c != '\r')
                    sb.Append(c);
            }

            return sb.ToString();
        }

        

        private static void SendLineUdp(Socket udpSocket, EndPoint ep, string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message + "\r\n");
            udpSocket.SendTo(data, ep);
        }

        private static string ReceiveLineUdp(Socket udpSocket, ref EndPoint ep)
        {
            byte[] buf = new byte[4096];
            int len = udpSocket.ReceiveFrom(buf, ref ep);
            string text = Encoding.UTF8.GetString(buf, 0, len);
            return text.TrimEnd('\r', '\n');
        }

        private static bool TryParseUdpPort(string okLine, out int port)
        {
            port = 0;

            
            string[] parts = okLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (parts[i].Equals("UDPPORT", StringComparison.OrdinalIgnoreCase))
                {
                    return int.TryParse(parts[i + 1], out port);
                }
            }
            return false;
        }
    }
}
