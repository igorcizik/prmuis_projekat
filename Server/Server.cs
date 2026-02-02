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

        private const int TCPPort = 9000;
        private const int UDPMin = 12000;
        private const int UDPMax = 13000;

        private static readonly HashSet<int> UsedUdpPorts = new HashSet<int>();
        private static readonly Random Rng = new Random();

        private static readonly List<Korisnik> Korisnici = new List<Korisnik>
        {
            new Korisnik("Nikola", "Bazic" , "nikolab" , "1234"),
            new Korisnik("Luka" , "Lukic", "lukal" , "1234"),
            new Korisnik("Marko" , "Markovic" , "markom" , "abcd"),
            new Korisnik("Isidora" , "Nikolic" , "isidoran", "7891"),


        };

        static void Main(string[] args)
        {
            /*Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint serverEP = new IPEndPoint(IPAddress.Any,TCPPort);
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
            serverSocket.Close();*/

            try
            {
                Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, TCPPort);
                serverSocket.Bind(serverEP);
                Korisnik ulogovanKorisnik = new Korisnik();



                serverSocket.Listen(10);

                Console.WriteLine($"Server slusa na portu {TCPPort}...");

                Socket acceptedSocket = serverSocket.Accept();

                Console.WriteLine("Klijent povezan " + acceptedSocket.RemoteEndPoint);

                UdpClient udpSocket = new UdpClient();  
                udpSocket.Client.ReceiveTimeout = 5000;

                SendLineTcp(acceptedSocket, "==============LOGOVANJE==============");
                SendLineTcp(acceptedSocket, "Username:");

                string username = ReceiveLineTcp(acceptedSocket);
                if (string.IsNullOrWhiteSpace(username))
                {
                    SendLineTcp(acceptedSocket, "ERR empty_username");
                    return;
                }

                SendLineTcp(acceptedSocket, "Sifra:");
                string password = ReceiveLineTcp(acceptedSocket);
                if (string.IsNullOrWhiteSpace(password))
                {
                    SendLineTcp(acceptedSocket, "ERR empty_password");
                    return;
                }

                ulogovanKorisnik = Korisnici.FirstOrDefault(u =>
                    u.KorisnickoIme.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));

                if (ulogovanKorisnik == null)
                {
                    SendLineTcp(acceptedSocket, "ERR user_not_found");
                    return;
                }

                if (ulogovanKorisnik.StatusPrijave)
                {
                    SendLineTcp(acceptedSocket, "ERR already_logged_in");
                    return;
                }

                if (ulogovanKorisnik.Lozinka != password)
                {
                    SendLineTcp(acceptedSocket, "ERR wrong_password");
                    return;
                }

                int sessionUdpPort = DodeliUDPPort();
                ulogovanKorisnik.StatusPrijave = true;
                ulogovanKorisnik.Port = sessionUdpPort;

                SendLineTcp(acceptedSocket, $"OK {ulogovanKorisnik.Ime} {ulogovanKorisnik.Prezime} UDPPORT {sessionUdpPort}");
                Console.WriteLine($"[LOGIN] OK: {ulogovanKorisnik.KorisnickoIme}, dodeljen UDP port {sessionUdpPort}");

                


            }
            catch (SocketException ex)
            {
                Console.WriteLine("[ERROR] SocketException: " + ex.Message);
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

        private static int DodeliUDPPort()
        {
            for (int i = 0; i < 5000; i++)
            {
                int p = Rng.Next(UDPMin, UDPMax + 1);
                if (!UsedUdpPorts.Contains(p))
                {
                    UsedUdpPorts.Add(p);
                    return p;
                }
            }
            throw new Exception("Nema slobodnih UDP portova u opsegu.");
        }
    }


}
