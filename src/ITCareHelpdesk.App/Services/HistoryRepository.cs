using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Services;

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
}
