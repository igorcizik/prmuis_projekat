using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;

namespace Server
{
    public class Program
    {
        private const int TCPPort = 9000;
        private const int UDPMin = 12000;
        private const int UDPMax = 13000;

        // sesija: 3 minuta neaktivnosti
        private const int SessionIdleMs = 3 * 60 * 1000; 
        private const int UdpPollTimeoutUs = 300 * 1000; 

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
            Socket serverSocket = null;

            try
            {
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                
                serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

                serverSocket.Bind(new IPEndPoint(IPAddress.Any, TCPPort));
                serverSocket.Listen(10);

                Console.WriteLine($"Server slusa na portu {TCPPort}...");

                while (true) 
                {
                    Socket acceptedSocket = null;
                    Socket udpSocket = null;      
                    Socket udpUserSocket = null;  

                    Korisnik ulogovanKorisnik = null;
                    int sessionUdpPort = 0;

                    try
                    {
                        acceptedSocket = serverSocket.Accept();
                        Console.WriteLine("Klijent povezan " + acceptedSocket.RemoteEndPoint);

                        // UDP socket za kom sa uredjajima
                        udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        udpSocket.Bind(new IPEndPoint(IPAddress.Any, 0));
                        udpSocket.ReceiveTimeout = 5000;

                        
                        SendLineTcp(acceptedSocket, "==============LOGOVANJE==============");
                        SendLineTcp(acceptedSocket, "Username:");

                        string username = ReceiveLineTcp(acceptedSocket);
                        if (string.IsNullOrWhiteSpace(username))
                        {
                            SendLineTcp(acceptedSocket, "ERR empty_username");
                            continue;
                        }

                        SendLineTcp(acceptedSocket, "Sifra:");
                        string password = ReceiveLineTcp(acceptedSocket);
                        if (string.IsNullOrWhiteSpace(password))
                        {
                            SendLineTcp(acceptedSocket, "ERR empty_password");
                            continue;
                        }

                        ulogovanKorisnik = Korisnici.FirstOrDefault(u =>
                            u.KorisnickoIme.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));

                        if (ulogovanKorisnik == null)
                        {
                            SendLineTcp(acceptedSocket, "ERR user_not_found");
                            continue;
                        }

                        if (ulogovanKorisnik.StatusPrijave)
                        {
                            SendLineTcp(acceptedSocket, "ERR already_logged_in");
                            continue;
                        }

                        if (ulogovanKorisnik.Lozinka != password)
                        {
                            SendLineTcp(acceptedSocket, "ERR wrong_password");
                            continue;
                        }

                        sessionUdpPort = DodeliUDPPort();
                        ulogovanKorisnik.StatusPrijave = true;
                        ulogovanKorisnik.Port = sessionUdpPort;

                        SendLineTcp(acceptedSocket, $"OK {ulogovanKorisnik.Ime} {ulogovanKorisnik.Prezime} UDPPORT {sessionUdpPort}");
                        Console.WriteLine($"[LOGIN] OK: {ulogovanKorisnik.KorisnickoIme}, dodeljen UDP port {sessionUdpPort}");

                        
                        udpUserSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        udpUserSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                        udpUserSocket.Bind(new IPEndPoint(IPAddress.Any, sessionUdpPort));
                        udpUserSocket.Blocking = false;

                        EndPoint clientUserEP = new IPEndPoint(IPAddress.Any, 0);

                        
                        Console.WriteLine($"[UDP-USER] Cekam prvi UDP paket na {sessionUdpPort}...");
                        bool handshakeOk = WaitForFirstUdpPacket(udpUserSocket, ref clientUserEP, 3000);

                        if (!handshakeOk)
                        {
                            Console.WriteLine("[UDP-USER] Handshake timeout. Odjavljujem korisnika.");
                            CleanupSession(ulogovanKorisnik, sessionUdpPort);
                            continue;
                        }

                        Console.WriteLine($"[UDP-USER] Klijent UDP endpoint: {clientUserEP}");
                        SendLineUdp(udpUserSocket, clientUserEP, "UDP kanal uspostavljen.");
                        SendLineUdp(udpUserSocket, clientUserEP, "Komande: <Uredjaj> <funkcija:vrednost> | 'kraj' za izlaz");

                        ispisListeUredjaja(ListaUredjaja, udpUserSocket, clientUserEP);

                        
                        Stopwatch idleSw = new Stopwatch();
                        idleSw.Start();

                        
                        while (true)
                        {
                            // istek sesije (1 min bez poruke)
                            if (idleSw.ElapsedMilliseconds >= SessionIdleMs)
                            {
                                try
                                {
                                    SendLineUdp(udpUserSocket, clientUserEP,
                                        "SESSION_CLOSED (istek 1min neaktivnosti) - prijavi se ponovo.");
                                }
                                catch { }

                                Console.WriteLine($"[SESSION] Korisnik '{ulogovanKorisnik.KorisnickoIme}' je odjavljen (istek sesije).");

                                CleanupSession(ulogovanKorisnik, sessionUdpPort);

                                
                                try { udpUserSocket.Close(); } catch { }
                                try { acceptedSocket.Close(); } catch { }

                                break; // nazad na Accept()
                            }

                            
                            if (!udpUserSocket.Poll(UdpPollTimeoutUs, SelectMode.SelectRead))
                                continue;

                            string komanda;
                            try
                            {
                                komanda = ReceiveLineUdp(udpUserSocket, ref clientUserEP);
                            }
                            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                            {
                                continue;
                            }

                            if (komanda == null)
                                break;

                            

                            
                            idleSw.Restart();

                            if (komanda.Equals("kraj", StringComparison.OrdinalIgnoreCase))
                            {
                                try { SendLineUdp(udpUserSocket, clientUserEP, "Odjavljeni ste. Prijavite se ponovo."); } catch { }
                                Console.WriteLine($"[LOGOUT] Korisnik '{ulogovanKorisnik.KorisnickoIme}' se odjavio (kraj).");

                                CleanupSession(ulogovanKorisnik, sessionUdpPort);

                                try { udpUserSocket.Close(); } catch { }
                                try { acceptedSocket.Close(); } catch { }

                                break; // nazad na Accept()
                            }

                            
                            string[] delovi = komanda.Split(' ');
                            if (delovi.Length != 2)
                            {
                                SendLineUdp(udpUserSocket, clientUserEP, "Pogresan format: <Uredjaj> <funkcija:vrednost>");
                                continue;
                            }

                            string imeUredjaja = delovi[0];
                            string[] fv = delovi[1].Split(':');
                            if (fv.Length != 2)
                            {
                                SendLineUdp(udpUserSocket, clientUserEP, "Pogresan format funkcije: funkcija:vrednost");
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

                                        IPEndPoint deviceEP = new IPEndPoint(IPAddress.Loopback, u.Port);
                                        string udpMsg = funkcija + ":" + vrednost;
                                        byte[] data = Encoding.UTF8.GetBytes(udpMsg);

                                        udpSocket.SendTo(data, deviceEP);

                                        try
                                        {
                                            byte[] respBuf = new byte[2048];
                                            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                                            int respLen = udpSocket.ReceiveFrom(respBuf, ref from);
                                            string respText = Encoding.UTF8.GetString(respBuf, 0, respLen);

                                            SendLineUdp(udpUserSocket, clientUserEP, "ACK " + respText);
                                        }
                                        catch (SocketException)
                                        {
                                            SendLineUdp(udpUserSocket, clientUserEP, "ACK TIMEOUT (uredjaj ne odgovara)");
                                        }
                                    }
                                    else
                                    {
                                        SendLineUdp(udpUserSocket, clientUserEP, $"Uredjaj nema funkciju: {funkcija}");
                                    }

                                    break;
                                }
                            }

                            if (!uredjajPronadjen)
                                SendLineUdp(udpUserSocket, clientUserEP, "Nepostojeci uredjaj");

                            ispisListeUredjaja(ListaUredjaja, udpUserSocket, clientUserEP);
                        }
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine("[ERROR] SocketException (client): " + ex.Message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[ERROR] Exception (client): " + ex.Message);
                    }
                    finally
                    {
                        
                        try
                        {
                            if (ulogovanKorisnik != null && ulogovanKorisnik.StatusPrijave && sessionUdpPort != 0)
                            {
                                Console.WriteLine($"[CLEANUP] Forsiram odjavu za {ulogovanKorisnik.KorisnickoIme} (finally).");
                                CleanupSession(ulogovanKorisnik, sessionUdpPort);
                            }
                        }
                        catch { }

                        try { udpUserSocket?.Close(); } catch { }
                        try { udpSocket?.Close(); } catch { }
                        try { acceptedSocket?.Close(); } catch { }
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("[ERROR] SocketException: " + ex.Message);
            }
            finally
            {
                try { serverSocket?.Close(); } catch { }
            }
        }

        private static bool WaitForFirstUdpPacket(Socket udpUserSocket, ref EndPoint clientUserEP, int maxWaitMs)
        {
            Stopwatch sw = Stopwatch.StartNew();
            byte[] buf = new byte[2048];

            while (sw.ElapsedMilliseconds < maxWaitMs)
            {
                if (udpUserSocket.Poll(200 * 1000, SelectMode.SelectRead)) // 200ms
                {
                    try
                    {
                        udpUserSocket.ReceiveFrom(buf, ref clientUserEP);
                        return true;
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                    {
                        // ignore
                    }
                }
            }
            return false;
        }

        private static void CleanupSession(Korisnik k, int udpPort)
        {
            try
            {
                if (k != null)
                {
                    k.StatusPrijave = false;
                    k.Port = 0;
                }
                if (udpPort != 0)
                {
                    UsedUdpPorts.Remove(udpPort);
                }
            }
            catch { }
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

        private static void ispisListeUredjaja(List<Uredjaj> listaZaIspis, Socket udpUserSocket, EndPoint clientEP)
        {
            int i = 1;
            SendLineUdp(udpUserSocket, clientEP, "Lista uredjaja:");
            foreach (Uredjaj u in listaZaIspis)
            {
                string ispis = i + ") " + u.ImeUredjaja + " " + u.Port + " ";
                foreach (var par in u.Funkcije)
                    ispis = ispis + par.Key + ":" + par.Value + " ";

                SendLineUdp(udpUserSocket, clientEP, ispis);
                i++;
            }
            SendLineUdp(udpUserSocket, clientEP, "Kraj liste uredjaja.");
        }
    }
}
