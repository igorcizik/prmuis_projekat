using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Kapija
{
    
    internal class Kapija
    {
        static string otvorena = "ne";
        static void Main(string[] args)
        {
            int PortUredjaja = 10003;

            UdpClient udpKlijent = new UdpClient(PortUredjaja);

            Console.WriteLine("Uredjaj kapija pokrenut");
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
                case "otvorena":
                    otvorena = vrednost;
                    return "Kapija: otvorena: " + vrednost;

                default:
                    return "ERROR nepostojeca funkcija";
            }

        }
    }
}
