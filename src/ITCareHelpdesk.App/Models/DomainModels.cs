using System;

namespace ITCareHelpdesk.App.Models;

// Aici tinem toate modelele simple (POCO-uri) intr-un singur fisier.
// Avantajul este ca vezi rapid toate structurile aplicatiei fara sa deschizi
// 20 de fisiere separate.
//
// Aceste modele NU sunt entitati Entity Framework.
// Sunt doar obiecte simple folosite pentru transfer de date intre SQL si UI.
//
// Folosim "record" pentru ca:
// - genereaza automat constructor
// - compara valorile usor
// - reduce codul boilerplate
// - modelele devin immutable implicit
//
// "sealed" inseamna ca nu pot fi mostenite.
// Aici nu avem nevoie de inheritance si evitam complexitate inutila.

public sealed record AppUser(
    // ID unic utilizator din baza de date
    int UserId,

    // Username-ul folosit la autentificare
    string Username,

    // Rolul utilizatorului (Admin, Technician etc.)
    string Role,

    // Legatura optionala catre tehnician
    int? TehnicianId,

    // Numele complet pentru afisare in UI
    string? NumeComplet,

    // Email utilizator
    string? Email,

    // Data cand a fost creat contul
    DateTime DataCreare
);

public sealed record Ticket(
    // ID intern tichet
    int TichetId,

    // Cod vizibil de forma TIC-2026-001
    string NumarTichet,

    // Titlul scurt al problemei
    string Titlu,

    // Descriere detaliata
    string? Descriere,

    // Numele clientului
    string Client,

    // Categoria tichetului
    string Categorie,

    // Departamentul asociat
    string Departament,

    // Nivel prioritate
    string Prioritate,

    // Status curent
    string Status,

    // Tip tichet (incident/request etc.)
    string Tip,

    // Numele tehnicianului asignat
    string Tehnician,

    // Nivel tehnician (Junior/Senior etc.)
    string? NivelTehnician,

    // Tip SLA aplicat
    string? TipSla,

    // Timp maxim raspuns permis
    int? TimpRaspunsOre,

    // Timp maxim rezolvare permis
    int? TimpRezolvareOre,

    // Cate ore a stat tichetul deschis
    int OreDeschis,

    // Daca SLA-ul a fost depasit
    bool SlaDepasit,

    // Data deschidere tichet
    DateTime DataDeschidere,

    // Data rezolvare
    DateTime? DataRezolvare,

    // Data inchidere finala
    DateTime? DataInchidere,

    // Total ore lucrate pe tichet
    decimal? OreLucrate,

    // Rating oferit de client
    int? RatingClient
);

public sealed record Client(
    // ID unic client
    int ClientId,

    // Nume companie
    string NumeCompanie,

    // Domeniul companiei
    string? Industrie,

    // Oras sediu
    string? Oras,

    // Telefon contact
    string? Telefon,

    // Email contact principal
    string? EmailContact,

    // Data incepere contract
    DateTime DataContract,

    // Daca clientul este activ
    bool Activ,

    // Cate tichete are in sistem
    int NrTichete
);

public sealed record Technician(
    // ID tehnician
    int TehnicianId,

    // Cod intern tehnician
    string CodTehnician,

    // Nume familie
    string Nume,

    // Prenume
    string Prenume,

    // Email tehnician
    string Email,

    // Specializare principala
    string Specializare,

    // Nivel experienta
    string Nivel,

    // Total tichete procesate
    int TotalTichete,

    // Cate tichete au fost rezolvate
    int TicheteRezolvate,

    // Cate sunt inca deschise
    int TicheteDeschise,

    // Timp mediu rezolvare
    decimal? TimpMediuRezolvareOre,

    // Rating mediu primit
    decimal? RatingMediu,

    // Pozitia in clasament
    int? LocClasament
);

public sealed record Asset(
    // ID asset
    int AssetId,

    // Cod inventar
    string CodAsset,

    // Denumire asset
    string Denumire,

    // Tip asset
    string Tip,

    // Firma producatoare
    string? Producator,

    // Model hardware
    string? Model,

    // Clientul caruia ii apartine
    string Client,

    // Locatia fizica
    string? Locatie,

    // Status asset
    string Status,

    // Garantie expirare
    DateTime? GarantiePana
);

public sealed record CategoryStat(
    // Categoria analizata
    string Categorie,

    // Departamentul categoriei
    string Departament,

    // Total tichete
    int TotalTichete,

    // Cate sunt rezolvate
    int Rezolvate,

    // Procent rezolvare
    decimal? ProcentRezolvate,

    // Timp mediu lucru
    decimal? TimpMediuOre,

    // Total ore lucrate
    decimal? TotalOreLucrate
);

public sealed record HistoryEntry(
    // Cod tichet
    string NumarTichet,

    // Client asociat
    string Client,

    // Tip actiune efectuata
    string TipActivitate,

    // Mesaj istoric
    string Mesaj,

    // Status vechi
    string? StatusVechi,

    // Status nou
    string? StatusNou,

    // Cine a facut actiunea
    string EfectuatDe,

    // Ore lucrate la acea actiune
    decimal? OreLucrate,

    // Data activitate formatata
    string DataActivitate
);

public sealed record DashboardKpi(
    // Cate tichete sunt deschise acum
    int TicheteDeschise,

    // Cate au depasit termenul
    int TicheteIntarziate,

    // Cate au fost rezolvate azi
    int TicheteRezolvateAzi,

    // Numar clienti activi
    int ClientiActivi,

    // Media satisfactiei clientilor
    decimal SatisfactieMedia,

    // Procent respectare SLA
    int SlaCompliancePercent
);

// Folosit pentru dropdown-ul de categorii la creare tichet.
// Nu trimitem modelul complet Category din DB pentru ca UI-ul
// nu are nevoie de toate campurile.

public sealed record CategoryOption(
    // ID categorie
    int CategorieId,

    // Numele categoriei
    string NumeCategorie,

    // Departamentul asociat
    string NumeDepartament
)
{
    // Text afisat in ComboBox.
    // Exemplu:
    // (Network) VPN
    public string Display => $"({NumeDepartament})  {NumeCategorie}";
}

public sealed record TechnicianOption(
    // ID tehnician
    int TehnicianId,

    // Nume complet
    string Nume,

    // Specializare
    string Specializare
)
{
    // Text afisat in dropdown
    public string Display => $"{Nume}  ·  {Specializare}";
}

// Structura simpla pentru donut chart.
// Fiecare status are un numar asociat.
public sealed record StatusBucket(
    string Status,
    int Count
);

// Structura pentru sparkline chart.
// Eticheta = zi/luna etc.
// Count = cate tichete.
public sealed record DailyResolved(
    string Eticheta,
    int Count
);

// Reprezinta o celula din heatmap.
// ZiSaptamana:
// 1 = Duminica
// 7 = Sambata
//
// Ora:
// 0-23
//
// NrTichete:
// cate tichete exista in acel interval.
public sealed record HeatmapCell(
    int ZiSaptamana,
    int Ora,
    int NrTichete
);