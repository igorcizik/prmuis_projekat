using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class Uredjaj
    {

        public string ImeUredjaja { get; set; }
        public int Port { get; set; }
        public Dictionary<string, string> Funkcije { get; set; }

        public DateTime PoslednjaPromena { get; set; }

        public Uredjaj() { this.Funkcije = new Dictionary<string, string>(); }
       
        public Uredjaj(string imeUredjaja, int port, Dictionary<string,string> pocetnoStanje)
        {;
            this.ImeUredjaja = imeUredjaja;
            this.Port = port;
            this.Funkcije = pocetnoStanje;
            this.PoslednjaPromena= DateTime.Now;   
        }

        }
    }      

