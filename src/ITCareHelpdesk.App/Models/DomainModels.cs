using System;

namespace ITCareHelpdesk.App.Models;

// Tinem toate POCO-urile intr-un singur fisier pentru ca:
// 1) sunt simple si nu beneficiaza de file-per-class pentru mentenanta
// 2) cand le citesti vrei sa vezi cum se "imbina" — un singur scroll, gata
//
// NU sunt entity-uri ORM, sunt doar shape-uri folosite cu ADO.NET reader-ul.
// Daca pe viitor migrezi la EF Core, le adnotezi cu atribute si gata.

public sealed record AppUser(
    int      UserId,
    string   Username,
    string   Role,
    int?     TehnicianId,
    string?  NumeComplet,
    string?  Email,
    DateTime DataCreare);

public sealed record Ticket(
    int       TichetId,
    string    NumarTichet,
    string    Titlu,
    string?   Descriere,
    string    Client,
    string    Categorie,
    string    Departament,
    string    Prioritate,
    string    Status,
    string    Tip,
    string    Tehnician,
    string?   NivelTehnician,
    string?   TipSla,
    int?      TimpRaspunsOre,
    int?      TimpRezolvareOre,
    int       OreDeschis,
    bool      SlaDepasit,
    DateTime  DataDeschidere,
    DateTime? DataRezolvare,
    DateTime? DataInchidere,
    decimal?  OreLucrate,
    int?      RatingClient);

public sealed record Client(
    int     ClientId,
    string  NumeCompanie,
    string? Industrie,
    string? Oras,
    string? Telefon,
    string? EmailContact,
    DateTime DataContract,
    bool     Activ,
    int      NrTichete);

public sealed record Technician(
    int     TehnicianId,
    string  CodTehnician,
    string  Nume,
    string  Prenume,
    string  Email,
    string  Specializare,
    string  Nivel,
    int     TotalTichete,
    int     TicheteRezolvate,
    int     TicheteDeschise,
    decimal? TimpMediuRezolvareOre,
    decimal? RatingMediu,
    int?    LocClasament);

public sealed record Asset(
    int      AssetId,
    string   CodAsset,
    string   Denumire,
    string   Tip,
    string?  Producator,
    string?  Model,
    string   Client,
    string?  Locatie,
    string   Status,
    DateTime? GarantiePana);

public sealed record CategoryStat(
    string   Categorie,
    string   Departament,
    int      TotalTichete,
    int      Rezolvate,
    decimal? ProcentRezolvate,
    decimal? TimpMediuOre,
    decimal? TotalOreLucrate);

public sealed record HistoryEntry(
    string   NumarTichet,
    string   Client,
    string   TipActivitate,
    string   Mesaj,
    string?  StatusVechi,
    string?  StatusNou,
    string   EfectuatDe,
    decimal? OreLucrate,
    string   DataActivitate);

public sealed record DashboardKpi(
    int     TicheteDeschise,
    int     TicheteIntarziate,
    int     TicheteRezolvateAzi,
    int     ClientiActivi,
    decimal SatisfactieMedia,
    int     SlaCompliancePercent);

// Optiuni pentru dropdown-uri din formularul de creare tichet — separate de modelele
// "grele" (Client, Technician) ca sa transmitem doar ce vede UI-ul, nu campuri sensibile.
public sealed record CategoryOption(
    int    CategorieId,
    string NumeCategorie,
    string NumeDepartament)
{
    // Afisat in ComboBox: "(Network) Conectivitate VPN"
    public string Display => $"({NumeDepartament})  {NumeCategorie}";
}

public sealed record TechnicianOption(
    int    TehnicianId,
    string Nume,
    string Specializare)
{
    public string Display => $"{Nume}  ·  {Specializare}";
}

public sealed record StatusBucket(string Status, int Count);
public sealed record DailyResolved(string Eticheta, int Count);
