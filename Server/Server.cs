using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
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

        private static List<Uredjaj> ListaUredjaja = new List<Uredjaj>()
        {
            new Uredjaj("Svetla",10001,new Dictionary<string,string>{{"power","OFF"}, {"intenzitet","70"}, {"boja","bela"} }),
            new Uredjaj("Klima",10002, new Dictionary<string, string>{{"power","OFF"}, { "mode", "grejanje"}, { "temp", "22"} }),
            new Uredjaj("Kapija",10003, new Dictionary<string, string>{{"otvorena","ne"}}),
        };

        static void Main(string[] args)
        {

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

                ispisListeUredjaja(ListaUredjaja, acceptedSocket);
                Console.WriteLine("[ISPIS] Ispisana lista dostupnih uredjaja korisniku");

                SendLineTcp(acceptedSocket, "Dostupno slanje komandi (unesi kraj za izlaz)\n");
                while (true)
                {
                    string komanda = ReceiveLineTcp(acceptedSocket);
                   // Console.WriteLine($"{komanda}");

                    if (komanda == null)
                        break;

                    if (komanda == "kraj")
                    {
                        Console.ReadKey();
                        return;
                    }

                    string[] delovi = komanda.Split(' ');

                    if (delovi.Length != 2)
                    {
                        SendLineTcp(acceptedSocket, "Pogresan format komande");
                        continue;
                    }

                    string imeUredjaja = delovi[0]; 

                    
                    string[] fv = delovi[1].Split(':');

                    if (fv.Length != 2)
                    {
                        SendLineTcp(acceptedSocket, "Pogresan format funkcije");
                        continue;
                    }

                    string funkcija = fv[0]; 
                    string vrednost = fv[1];


                    bool uredjajPronadjen = false;

                    foreach (var u in ListaUredjaja)
                    {
                        if (u.ImeUredjaja.Equals(imeUredjaja, StringComparison.OrdinalIgnoreCase))
                        {
                            uredjajPronadjen = true;

                            if (u.Funkcije.ContainsKey(funkcija))
                            {
                                u.Funkcije[funkcija] = vrednost;

                                SendLineTcp(acceptedSocket,
                                    $"{imeUredjaja} {funkcija} postavljeno na {vrednost}");
                            }
                            else
                            {
                                SendLineTcp(acceptedSocket,
                                    $"Uredjaj nema funkciju: {funkcija}");
                            }

                            break; 
                        }
                    }

                    if (!uredjajPronadjen)
                    {
                        SendLineTcp(acceptedSocket, "Nepostojeci uredjaj");
                    }

                    ispisListeUredjaja(ListaUredjaja, acceptedSocket);

                }

                


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

        private static void ispisListeUredjaja(List<Uredjaj> listaZaIspis,Socket accSocket)
        {
            int i = 1;
            SendLineTcp(accSocket,"Lista uredjaja:\n");
            foreach(Uredjaj u in listaZaIspis)
            {
                string ispis = String.Empty;

                ispis = i+ ")" + " " + u.ImeUredjaja + " " + u.Port + " ";
                foreach(var par in u.Funkcije)
                {
                    ispis = ispis + par.Key + ":" + par.Value + " ";
                }
                SendLineTcp(accSocket, ispis);
                i++;
            }

            SendLineTcp(accSocket, "\nKraj liste uredjaja.\n");


        }
    }


}
