using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Klima
{
    internal class Program
    {
        static string power = "OFF";
        static string mode = "grejanje";
        static string temp = "22";

        static void Main(string[] args)
        {
            int PortUredjaja = 10002;

            UdpClient udpKlijent = new UdpClient(PortUredjaja);

            Console.WriteLine("Uredjaj klima pokrenut");
            Console.WriteLine("Slusam na UDP portu: " + PortUredjaja);

            while (true)
            {
                IPEndPoint serverEP = new IPEndPoint(IPAddress.Any, 0);

                byte[] primljenaPoruka = udpKlijent.Receive(ref serverEP);

                string komanda = Encoding.UTF8.GetString(primljenaPoruka);

                Console.WriteLine("Primljeno: " + komanda);

                string odgovor = ObradiKomandu(komanda);

                byte[] povratnaPoruka = Encoding.UTF8.GetBytes(odgovor);

                udpKlijent.Send(povratnaPoruka, povratnaPoruka.Length, serverEP);

            }
        }

        private static string ObradiKomandu(string komanda)
        {


            string[] delovi = komanda.Split(':');

            if (delovi.Length != 2)
            {
                return "pogresan format";
            }

            string funkcija = delovi[0];
            string vrednost = delovi[1];

            switch (funkcija)
            {
                case "power":
                    power = vrednost;
                    return "Klima: power "+power;

                case "mode":
                    mode = vrednost;
                    return "Klima: mod promenjen u " + mode;

                case "temp":
                    temp = vrednost;
                    return "Klima: temperatura postavljena na " + temp + "°C";

                default:
                    return "ERROR nepostojeca funkcija";
            }

        }
    }
}
