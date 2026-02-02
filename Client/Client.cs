using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    internal class Client
    {
        static void Main(string[] args)
        {



            Socket clientSocket = null;
            

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
                    string serverLine = ReceiveLineTcp(clientSocket);
                    if (serverLine == null)
                    {
                        Console.WriteLine("Server je zatvorio konekciju.");
                        Console.WriteLine("Pritisni taster za izlaz...");
                        Console.ReadKey();

                        return;
                    }

                    Console.WriteLine(serverLine);

                    if (serverLine.StartsWith("ERR", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Login neuspesan. Klijent zavrsava.");
                        Console.WriteLine("Pritisni taster za izlaz...");
                        Console.ReadKey();

                        return;
                    }

                    if (serverLine.Trim().Equals("Username:", StringComparison.OrdinalIgnoreCase))
                    {
                        
                        string username = Console.ReadLine();
                        SendLineTcp(clientSocket, username);
                    }
                    else if (serverLine.Trim().Equals("Sifra:", StringComparison.OrdinalIgnoreCase))
                    {
                        
                        string pass = Console.ReadLine();
                        SendLineTcp(clientSocket, pass);
                    }
                    

                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Pritisni taster za izlaz...");
                Console.ReadKey();

                Console.WriteLine("Socket greska: " + ex.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Pritisni taster za izlaz...");
                Console.ReadKey();

                Console.WriteLine("Greska: " + ex);
            }
            Console.WriteLine("Pritisni taster za izlaz...");
            Console.ReadKey();

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
    }
}

