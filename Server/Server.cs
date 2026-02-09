using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;

namespace Server
{
    public class Program
    {
        private const int TCPPort = 9000;
        private const int UDPMin = 12000;
        private const int UDPMax = 13000;

        private const int SessionIdleMs = 3 * 60 * 1000;
        private const int UdpPollTimeoutUs = 300 * 1000;

        private const string DevicesStateFile = "devices_state.json";
        private const string ActivityLogFile = "activity_log.txt";

        private static readonly HashSet<int> UsedUdpPorts = new HashSet<int>();
        private static readonly Random Rng = new Random();

        private static readonly object ConsoleLock = new object();
        private static readonly Dictionary<string, DateTime> LastUserActivity = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        private static readonly List<Korisnik> Korisnici = new List<Korisnik>
        {
            new Korisnik("Nikola","Bazic","nikolab","1234"),
            new Korisnik("Luka","Lukic","lukal","1234"),
            new Korisnik("Marko","Markovic","markom","abcd"),
            new Korisnik("Isidora","Nikolic","isidoran","7891")
        };

        private static List<Uredjaj> ListaUredjaja = new List<Uredjaj>
        {
            new Uredjaj("Svetla",10001,new Dictionary<string,string>{{"power","OFF"},{"intenzitet","70"},{"boja","bela"}}),
            new Uredjaj("Klima",10002,new Dictionary<string,string>{{"power","OFF"},{"mode","grejanje"},{"temp","22"}}),
            new Uredjaj("Kapija",10003,new Dictionary<string,string>{{"otvorena","ne"}})
        };

        static void Main(string[] args)
        {
            LoadDevicesState();
            DisplaySystemStatus("SERVER START");

            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            serverSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            serverSocket.Bind(new IPEndPoint(IPAddress.Any, TCPPort));
            serverSocket.Listen(10);

            while (true)
            {
                Socket tcpClient = null;
                Socket udpUser = null;

                Dictionary<int, Socket> deviceSockets = null;
                List<Socket> deviceSocketList = null;

                Korisnik user = null;
                int udpPort = 0;

                try
                {
                    tcpClient = serverSocket.Accept();

                    user = Login(tcpClient);
                    if (user == null)
                    {
                        try { tcpClient.Close(); } catch { }
                        DisplaySystemStatus("LOGIN FAILED");
                        continue;
                    }

                    udpPort = AssignUdpPort();
                    user.StatusPrijave = true;
                    user.Port = udpPort;

                    LastUserActivity[user.KorisnickoIme] = DateTime.Now;

                    SendLineTcp(tcpClient, "OK " + user.Ime + " " + user.Prezime + " UDPPORT " + udpPort);

                    udpUser = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                    udpUser.Bind(new IPEndPoint(IPAddress.Any, udpPort));
                    udpUser.Blocking = false;

                    EndPoint clientEP = new IPEndPoint(IPAddress.Any, 0);
                    bool hs = WaitForFirstUdpPacket(udpUser, ref clientEP, 3000);
                    if (!hs)
                    {
                        Cleanup(user, udpPort);
                        try { udpUser.Close(); } catch { }
                        try { tcpClient.Close(); } catch { }
                        DisplaySystemStatus("UDP HANDSHAKE TIMEOUT");
                        continue;
                    }

                    deviceSockets = new Dictionary<int, Socket>();
                    deviceSocketList = new List<Socket>();
                    foreach (var dev in ListaUredjaja)
                    {
                        Socket s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                        s.Bind(new IPEndPoint(IPAddress.Any, 0));
                        s.Blocking = false;
                        deviceSockets[dev.Port] = s;
                        deviceSocketList.Add(s);
                    }

                    SendDevicesTable(udpUser, clientEP);
                    DisplaySystemStatus("LOGIN OK: " + user.KorisnickoIme);

                    Stopwatch idle = Stopwatch.StartNew();
                    Stopwatch dashSw = Stopwatch.StartNew();

                    while (true)
                    {
                        if (dashSw.ElapsedMilliseconds >= 1000)
                        {
                            DisplaySystemStatus("RUNNING");
                            dashSw.Restart();
                        }

                        if (idle.ElapsedMilliseconds >= SessionIdleMs)
                        {
                            try { SendLineUdp(udpUser, clientEP, "SESSION_CLOSED"); } catch { }
                            Cleanup(user, udpPort);
                            DisplaySystemStatus("SESSION TIMEOUT: " + user.KorisnickoIme);
                            break;
                        }

                        if (!udpUser.Poll(UdpPollTimeoutUs, SelectMode.SelectRead))
                            continue;

                        string cmd = ReceiveLineUdp(udpUser, ref clientEP);
                        idle.Restart();
                        LastUserActivity[user.KorisnickoIme] = DateTime.Now;

                        if (cmd == "kraj")
                        {
                            Cleanup(user, udpPort);
                            DisplaySystemStatus("LOGOUT: " + user.KorisnickoIme);
                            break;
                        }

                        Command(cmd, user, udpUser, clientEP, deviceSockets, deviceSocketList);
                    }
                }
                catch (SocketException ex)
                {
                    if (user != null && user.StatusPrijave && udpPort != 0) Cleanup(user, udpPort);
                    DisplaySystemStatus("SOCKET ERROR: " + ex.Message);
                }
                catch (Exception ex)
                {
                    if (user != null && user.StatusPrijave && udpPort != 0) Cleanup(user, udpPort);
                    DisplaySystemStatus("ERROR: " + ex.Message);
                }
                finally
                {
                    try { udpUser?.Close(); } catch { }
                    try { tcpClient?.Close(); } catch { }

                    if (deviceSocketList != null)
                    {
                        foreach (var s in deviceSocketList)
                        {
                            try { s?.Close(); } catch { }
                        }
                    }
                }
            }
        }

        static void Command(string cmd,Korisnik user,Socket udp,EndPoint ep,Dictionary<int, Socket> deviceSockets,List<Socket> deviceSocketList)
        {
            string[] p = cmd.Split(' ');
            if (p.Length != 2)
            {
                SendLineUdp(udp, ep, "Pogresan format komande");
                return;
            }

            string devName = p[0];
            string[] fv = p[1].Split(':');
            if (fv.Length != 2)
            {
                SendLineUdp(udp, ep, "Pogresan format funkcije");
                return;
            }

            Uredjaj d = ListaUredjaja.FirstOrDefault(x => x.ImeUredjaja.Equals(devName, StringComparison.OrdinalIgnoreCase));
            if (d == null)
            {
                SendLineUdp(udp, ep, "Nepostojeci uredjaj");
                return;
            }

            string funkcija = fv[0];
            string vrednost = fv[1];

            if (!d.Funkcije.ContainsKey(funkcija))
            {
                SendLineUdp(udp, ep, "Uredjaj nema funkciju: " + funkcija);
                return;
            }

            d.Funkcije[funkcija] = vrednost;
            d.PoslednjaPromena = DateTime.Now;

            SaveDevicesState();
            Log(user.KorisnickoIme, d.ImeUredjaja, funkcija, vrednost);

            IPEndPoint deviceEP = new IPEndPoint(IPAddress.Loopback, d.Port);
            byte[] data = Encoding.UTF8.GetBytes(funkcija + ":" + vrednost);

            Socket sendSocket;
            if (!deviceSockets.TryGetValue(d.Port, out sendSocket))
            {
                SendLineUdp(udp, ep, "Greska: nema socketa za uredjaj");
                return;
            }

            try { sendSocket.SendTo(data, deviceEP); }
            catch
            {
                SendLineUdp(udp, ep, "ACK TIMEOUT (uredjaj ne odgovara)");
                SendDevicesTable(udp, ep);
                DisplaySystemStatus("DEVICE SEND FAIL");
                return;
            }

            string odgovorUredjaja = null;
            byte[] buf = new byte[2048];
            Stopwatch sw = Stopwatch.StartNew();

            while (sw.ElapsedMilliseconds < 3000)
            {
                for (int i = 0; i < deviceSocketList.Count; i++)
                {
                    Socket s = deviceSocketList[i];
                    if (!s.Poll(200 * 1000, SelectMode.SelectRead))
                        continue;

                    EndPoint from = new IPEndPoint(IPAddress.Any, 0);
                    int len;
                    try
                    {
                        len = s.ReceiveFrom(buf, ref from);
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                    {
                        continue;
                    }

                    int fromPort = ((IPEndPoint)from).Port;
                    if (fromPort != d.Port)
                        continue;

                    odgovorUredjaja = Encoding.UTF8.GetString(buf, 0, len).Trim();
                    goto DONE_WAIT;
                }
            }

        DONE_WAIT:
            if (odgovorUredjaja == null)
                SendLineUdp(udp, ep, "ACK TIMEOUT (uredjaj ne odgovara)");
            else
                SendLineUdp(udp, ep, odgovorUredjaja);

            SendDevicesTable(udp, ep);
            DisplaySystemStatus("CMD: " + user.KorisnickoIme + " -> " + d.ImeUredjaja + " " + funkcija + ":" + vrednost);
        }

        static void DisplaySystemStatus(string status)
        {
            lock (ConsoleLock)
            {
                Console.Clear();
                Console.WriteLine("CENTRALNI SERVER - STANJE SISTEMA");
                Console.WriteLine("Vreme: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                Console.WriteLine("Status: " + status);
                Console.WriteLine();

                Console.WriteLine("AKTIVNI KORISNICI (UDP SESIJE)");
                Console.WriteLine("KorisnickoIme | Ime Prezime | UDP Port | Poslednja aktivnost");
                Console.WriteLine(new string('-', 78));

                var aktivni = Korisnici.Where(k => k.StatusPrijave).ToList();
                if (aktivni.Count == 0)
                {
                    Console.WriteLine("-");
                }
                else
                {
                    foreach (var k in aktivni)
                    {
                        DateTime last;
                        string lastStr = LastUserActivity.TryGetValue(k.KorisnickoIme, out last)
                            ? last.ToString("HH:mm:ss")
                            : "-";
                        Console.WriteLine($"{k.KorisnickoIme} | {k.Ime} {k.Prezime} | {k.Port} | {lastStr}");
                    }
                }

                Console.WriteLine();
                Console.WriteLine("STANJE UREDJAJA");
                Console.WriteLine("Uredjaj | Port | Funkcije | Poslednja promena");
                Console.WriteLine(new string('-', 78));

                foreach (var d in ListaUredjaja)
                {
                    string funcs = d.Funkcije != null && d.Funkcije.Count > 0
                        ? string.Join(", ", d.Funkcije.Select(x => x.Key + "=" + x.Value))
                        : "-";
                    string ts = d.PoslednjaPromena == default(DateTime)
                        ? "-"
                        : d.PoslednjaPromena.ToString("HH:mm:ss");
                    Console.WriteLine($"{d.ImeUredjaja} | {d.Port} | {funcs} | {ts}");
                }

                Console.WriteLine();
                Console.WriteLine("Log fajl: " + ActivityLogFile);
                Console.WriteLine("State fajl: " + DevicesStateFile);
            }
        }

        static void Log(string user, string dev, string f, string v)
        {
            try
            {
                File.AppendAllText(ActivityLogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} | {user} | {dev} | {f}:{v}\n");
            }
            catch { }
        }

        static void SaveDevicesState()
        {
            try
            {
                File.WriteAllText(DevicesStateFile, JsonConvert.SerializeObject(ListaUredjaja, Formatting.Indented));
            }
            catch { }
        }

        static void LoadDevicesState()
        {
            if (!File.Exists(DevicesStateFile)) return;

            try
            {
                var loaded = JsonConvert.DeserializeObject<List<Uredjaj>>(File.ReadAllText(DevicesStateFile));
                if (loaded == null || loaded.Count == 0) return;

                foreach (var baseDev in ListaUredjaja)
                {
                    var match = loaded.FirstOrDefault(d => d.ImeUredjaja != null &&
                                                          d.ImeUredjaja.Equals(baseDev.ImeUredjaja, StringComparison.OrdinalIgnoreCase));
                    if (match != null && match.Funkcije != null && match.Funkcije.Count > 0)
                    {
                        baseDev.Funkcije = match.Funkcije;
                        baseDev.PoslednjaPromena = match.PoslednjaPromena;
                    }
                }
            }
            catch { }
        }

        static void SendDevicesTable(Socket udp, EndPoint ep)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("IME | PORT | FUNKCIJE | POSLEDNJA PROMENA");
            sb.AppendLine("------------------------------------------");

            foreach (var d in ListaUredjaja)
            {
                sb.AppendLine($"{d.ImeUredjaja} | {d.Port} | " +
                              string.Join(",", d.Funkcije.Select(x => x.Key + "=" + x.Value)) +
                              " | " + d.PoslednjaPromena);
            }

            SendLineUdp(udp, ep, sb.ToString());
        }

        static Korisnik Login(Socket s)
        {
            SendLineTcp(s, "Username:");
            string u = ReceiveLineTcp(s);

            SendLineTcp(s, "Sifra:");
            string p = ReceiveLineTcp(s);

            var k = Korisnici.FirstOrDefault(x => x.KorisnickoIme == u && x.Lozinka == p);
            return k;
        }

        static void Cleanup(Korisnik k, int p)
        {
            try
            {
                if (k != null)
                {
                    k.StatusPrijave = false;
                    k.Port = 0;
                }
                UsedUdpPorts.Remove(p);
            }
            catch { }
        }

        static int AssignUdpPort()
        {
            int p;
            do { p = Rng.Next(UDPMin, UDPMax); } while (UsedUdpPorts.Contains(p));
            UsedUdpPorts.Add(p);
            return p;
        }

        static void SendLineTcp(Socket s, string m)
        {
            s.Send(Encoding.UTF8.GetBytes(m + "\r\n"));
        }

        static string ReceiveLineTcp(Socket s)
        {
            StringBuilder sb = new StringBuilder();
            byte[] b = new byte[1];
            while (s.Receive(b) > 0 && b[0] != '\n')
                if (b[0] != '\r') sb.Append((char)b[0]);
            return sb.ToString();
        }

        static void SendLineUdp(Socket s, EndPoint ep, string m)
        {
            s.SendTo(Encoding.UTF8.GetBytes(m + "\r\n"), ep);
        }

        static string ReceiveLineUdp(Socket s, ref EndPoint ep)
        {
            byte[] b = new byte[4096];
            int l = s.ReceiveFrom(b, ref ep);
            return Encoding.UTF8.GetString(b, 0, l).Trim();
        }

        static bool WaitForFirstUdpPacket(Socket s, ref EndPoint ep, int ms)
        {
            Stopwatch sw = Stopwatch.StartNew();
            byte[] b = new byte[256];
            while (sw.ElapsedMilliseconds < ms)
            {
                if (s.Poll(200000, SelectMode.SelectRead))
                {
                    try
                    {
                        s.ReceiveFrom(b, ref ep);
                        return true;
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
                    {
                    }
                }
            }
            return false;
        }
    }
}
