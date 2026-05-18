using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Services;

// ============================================================
// HistoryRepository
// ============================================================
// Repository pentru tabela "IstoricActivitate" — log-ul de business pe tichete.
// Fiecare actiune semnificativa (creare, schimbare status, comentariu, asignare, time-tracking)
// genereaza o intrare aici.
//
// Important: aceasta tabela e separata de "AuditTrail" (din 02_extensions.sql).
//   - IstoricActivitate = log de business, vizibil userilor, afisat in timeline
//   - AuditTrail = log tehnic generic, alimentat de trigger SQL, pentru audit/compliance
//
// Metode:
//   GetAsync          - intoarce istoricul filtrat (toate dimensiunile optionale)
//   GetForTicketAsync - shortcut pentru ecranul "Detail drawer" — luam istoricul unui singur
//                       tichet pe ultimele 365 zile (practic tot ce s-a intamplat)
//
// Apeleaza sp_GetIstoricActivitate (procedura SQL) care formateaza data ca string romanesc
// ("dd MMM yyyy HH:mm"), asa UI-ul nu trebuie sa stie de format.
// ============================================================
public sealed class HistoryRepository
{
    private readonly DatabaseService _db;
    public HistoryRepository(DatabaseService db) => _db = db;

    public Task<List<HistoryEntry>> GetAsync(int? clientId, int? tichetId, int days) =>
        _db.QueryAsync(
            "sp_GetIstoricActivitate",
            CommandType.StoredProcedure,
            r => new HistoryEntry(
                NumarTichet:    r.GetString(r.GetOrdinal("numar_tichet")),
                Client:         r.GetString(r.GetOrdinal("client")),
                TipActivitate:  r.GetString(r.GetOrdinal("tip_activitate")),
                Mesaj:          r.GetString(r.GetOrdinal("mesaj")),
                StatusVechi:    DatabaseService.GetNullableString(r, "status_vechi"),
                StatusNou:      DatabaseService.GetNullableString(r, "status_nou"),
                EfectuatDe:     r.GetString(r.GetOrdinal("efectuat_de")),
                OreLucrate:     DatabaseService.GetNullable<decimal>(r, "ore_lucrate"),
                DataActivitate: r.GetString(r.GetOrdinal("data_activitate"))),
            ("@client_id",   (object?)clientId),
            ("@tichet_id",   (object?)tichetId),
            ("@zile_inapoi", days));

    // Specializare pentru drawer — luam istoricul unui singur tichet pe ultimii 365 zile.
    // Folosim acelasi SP cu zile_inapoi marit la 365 ca sa luam tot.
    public Task<List<HistoryEntry>> GetForTicketAsync(int tichetId) =>
        GetAsync(clientId: null, tichetId: tichetId, days: 365);
}
