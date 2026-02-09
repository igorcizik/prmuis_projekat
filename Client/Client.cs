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
            const int tcpPort = 9000;
            IPAddress serverIp = IPAddress.Loopback;

            Console.WriteLine("Klijent je spreman. Pritisni ENTER da krenes...");
            Console.ReadLine();

            while (true) 
            {
                Socket clientSocket = null;
                Socket udpSock = null;
                EndPoint serverUdpEP = null;

                bool usingUdp = false;
                bool reloginNeeded = false;

                try
                {
                    
                    clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    IPEndPoint serverEP = new IPEndPoint(serverIp, tcpPort);

                    clientSocket.Connect(serverEP);
                    Console.WriteLine("Povezan na server (TCP).");

                    
                    while (true)
                    {
                        if (!usingUdp)
                        {
                            
                            string serverLine = ReceiveLineTcp(clientSocket);
                            if (serverLine == null)
                            {
                                Console.WriteLine("Server je zatvorio TCP konekciju.");
                                reloginNeeded = true;
                                break;
                            }

                            Console.WriteLine(serverLine);

                            if (serverLine.StartsWith("ERR", StringComparison.OrdinalIgnoreCase))
                            {
                                Console.WriteLine("Login neuspesan. Pokusavam ponovo (reconnect)...");
                                reloginNeeded = true;
                                break;
                            }

                            if (serverLine.Trim().Equals("Username:", StringComparison.OrdinalIgnoreCase))
                            {
                                string username = Console.ReadLine();
                                SendLineTcp(clientSocket, username);
                                continue;
                            }

                            if (serverLine.Trim().Equals("Sifra:", StringComparison.OrdinalIgnoreCase))
                            {
                                string pass = Console.ReadLine();
                                SendLineTcp(clientSocket, pass);
                                continue;
                            }

                            
                            if (serverLine.StartsWith("OK", StringComparison.OrdinalIgnoreCase) && serverLine.Contains("UDPPORT"))
                            {
                                if (!TryParseUdpPort(serverLine, out int udpPort))
                                {
                                    Console.WriteLine("Greska: ne mogu da procitam UDPPORT iz poruke: " + serverLine);
                                    reloginNeeded = true;
                                    break;
                                }

                                
                                udpSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                                udpSock.Bind(new IPEndPoint(IPAddress.Any, 0)); 
                                udpSock.Blocking = false;

                                serverUdpEP = new IPEndPoint(serverIp, udpPort);

                                // handshake
                                SendLineUdp(udpSock, serverUdpEP, "HELLO");

                                usingUdp = true;

                                Console.WriteLine($"UDP kanal inicijalizovan. Server UDP port: {udpPort}");
                                Console.WriteLine("Unosi komandu: <Uredjaj> <funkcija:vrednost> ili 'kraj'");
                                continue;
                            }
                        }
                        else
                        {
                            
                            while (udpSock.Poll(50 * 1000, SelectMode.SelectRead)) // 50ms
                            {
                                string udpLine = null;
                                try
                                {
                                    EndPoint from = serverUdpEP; 
                                    udpLine = ReceiveLineUdp(udpSock, ref from);
                                }
                                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                                {
                                    break;
                                }

                                if (udpLine == null) break;

                                Console.WriteLine(udpLine);

                                if (udpLine.StartsWith("SESSION_CLOSED", StringComparison.OrdinalIgnoreCase))
                                {
                                    
                                    reloginNeeded = true;
                                    break;
                                }
                            }

                            if (reloginNeeded)
                                break;

                            
                            if (Console.KeyAvailable)
                            {
                                string cmd = Console.ReadLine();

                                if (string.IsNullOrWhiteSpace(cmd))
                                    continue;

                                
                                SendLineUdp(udpSock, serverUdpEP, cmd);

                                if (cmd.Trim().Equals("kraj", StringComparison.OrdinalIgnoreCase))
                                {
                                    reloginNeeded = true;
                                    break;
                                }
                            }
                            else
                            {
                                
                                System.Threading.Thread.Sleep(30);
                            }
                        }
                    }
                }
                catch (SocketException ex)
                {
                    Console.WriteLine("Socket greska: " + ex.Message);
                    reloginNeeded = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Greska: " + ex);
                    reloginNeeded = true;
                }
                finally
                {
                    try { udpSock?.Close(); } catch { }
                    try { clientSocket?.Close(); } catch { }
                }

                if (reloginNeeded)
                {
                    Console.WriteLine();
                    Console.WriteLine("=== Potrebna je ponovna prijava (reconnect) ===");
                    Console.WriteLine("Pritisni ENTER za novi login (ili Ctrl+C za izlaz)...");
                    Console.ReadLine();
                    Console.Clear();
                    continue; 
                }

                break;
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
