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

          
            Socket clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            IPEndPoint serverEP = new IPEndPoint(IPAddress.Loopback, 9000);
            byte[] buffer = new byte[1024];

            Console.WriteLine("Klijent je spreman za povezivanje sa serverom, kliknite enter");
            Console.ReadKey();
            clientSocket.Connect(serverEP);
            Console.WriteLine("Klijent je uspesno povezan sa serverom!");
          
            while (true)
            {
               
                try
                {
                    string poruka = Console.ReadLine();
                    int brBajta = clientSocket.Send(Encoding.UTF8.GetBytes(poruka));

                    if (poruka == "kraj")
                        break;

                    brBajta = clientSocket.Receive(buffer);

                    if (brBajta == 0)
                    {
                        Console.WriteLine("Server je zavrsio sa radom");
                        break;
                    }

                    string odgovor = Encoding.UTF8.GetString(buffer);

                    Console.WriteLine(odgovor);
                    if (odgovor == "kraj")
                        break;

                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Doslo je do greske tokom slanja:\n{ex}");
                    break;
                }

            }
            
            Console.WriteLine("Klijent zavrsava sa radom");
            Console.ReadKey();
            clientSocket.Close();
           
        }
    }
}

