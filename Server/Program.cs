using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class Program
    {
        static void Main(string[] args)
        {
            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Any,9000);
            serverSocket.Bind(serverEP);

            serverSocket.Listen(10);

            Console.WriteLine($"Server je stavljen u stanje osluskivanja i ocekuje komunikaciju na {serverEP}");

            Socket acceptedSocket = serverSocket.Accept();

            

            IPEndPoint clientEP = acceptedSocket.RemoteEndPoint as IPEndPoint;
            Console.WriteLine($"Povezao se novi klijent! Njegova adresa je {clientEP}");


            byte[] buffer = new byte[1024];
            while (true)
            {
                try
                {
                    int brBajta = acceptedSocket.Receive(buffer);
                    if (brBajta == 0)
                    {
                        Console.WriteLine("Klijent je zavrsio sa radom");
                        break;
                    }
                    string poruka = Encoding.UTF8.GetString(buffer);
                    Console.WriteLine(poruka);


                    if (poruka == "kraj")
                        break;


                    Console.WriteLine("Unesite poruku");
                    string odgovor = Console.ReadLine();

                    brBajta = acceptedSocket.Send(Encoding.UTF8.GetBytes(odgovor));
                    if (odgovor == "kraj")
                        break;
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Doslo je do greske {ex}");
                    break;
                }

            }

            Console.WriteLine("Server zavrsava sa radom");
            Console.ReadKey();
            acceptedSocket.Close();
            serverSocket.Close();


        }
    }
}
