using System;

public class Korisnik
{
    public string Ime { get; set; }
    public string Prezime { get; set; }
    public string KorisnickoIme { get; set; }
    public string Lozinka { get; set; }
    public bool StatusPrijave { get; set; }
    public int Port { get; set; }

    public Korisnik() { }

    public Korisnik(string ime, string prezime, string korisnickoIme,
                    string lozinka)
    {
        Ime = ime;
        Prezime = prezime;
        KorisnickoIme = korisnickoIme;
        Lozinka = lozinka;
        StatusPrijave = false;
        Port = 0;
    }
}
