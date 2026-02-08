using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;


using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Svetla
{
    class Svetla
    {
        static string power = "OFF";
        static string intenzitet = "70";
        static string boja = "bela";

        static void Main(string[] args)
        {
            int PortUredjaja = 10001;

            Socket udpSocket = new Socket(AddressFamily.InterNetwork,SocketType.Dgram,ProtocolType.Udp);

            udpSocket.Bind(new IPEndPoint(IPAddress.Any, PortUredjaja));

            Console.WriteLine("Uredjaj svetla pokrenut");
            Console.WriteLine("Slusam na UDP portu: " + PortUredjaja);

            byte[] buffer = new byte[1024];

            while (true)
            {
                EndPoint serverEP = new IPEndPoint(IPAddress.Any, 0);

                int primljeno = udpSocket.ReceiveFrom(buffer, ref serverEP);

                string komanda = Encoding.UTF8.GetString(buffer, 0, primljeno);

                Console.WriteLine("Primljeno: " + komanda);

                string odgovor = ObradiKomandu(komanda);

                byte[] povratnaPoruka = Encoding.UTF8.GetBytes(odgovor);

                udpSocket.SendTo(povratnaPoruka, serverEP);
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
                    return "Svetla: power " + power;

                case "intenzitet":
                    intenzitet = vrednost;
                    return "Svetla: promena intenziteta na " + intenzitet + "%";

                case "boja":
                    boja = vrednost;
                    return "Svetla: boja promenjena u " + boja;

                default:
                    return "Svetla: nepostojeca funkcija";
            }
        }
    }
}
