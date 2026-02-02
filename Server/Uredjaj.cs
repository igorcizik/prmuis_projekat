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
        public Dictionary<string, int> Funkcije { get; set; }


        public Uredjaj() { this.Funkcije = new Dictionary<string, int>(); }

        public Uredjaj(string imeUredjaja, int port)
        {
            this.Funkcije = new Dictionary<string, int>();
            this.ImeUredjaja = imeUredjaja;
            this.Port = port;
            
        }

        public void dodajFunkciju(string funkcija)
        {
            if (!Funkcije.ContainsKey(funkcija))
            {
                Funkcije[funkcija] = 0;
            }
        }
     

        }
    }      

