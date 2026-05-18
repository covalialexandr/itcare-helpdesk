using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ITCareHelpdesk.App.Models;
using Microsoft.Data.SqlClient;

namespace ITCareHelpdesk.App.Services;

// Repository-uri pe entitate principala — tine apelurile catre stored procedures
// in dreptul lor, nu imprastiate prin ViewModels. Daca cineva schimba semnatura
// procedurii sp_DeschideTichet, vine aici si in 30 de secunde stie unde sa modifice.
public sealed class TicketRepository
{
    private readonly DatabaseService _db;
    public TicketRepository(DatabaseService db) => _db = db;

    public Task<List<Ticket>> GetActiveAsync() =>
        _db.QueryAsync(
            "SELECT * FROM vw_TicheteActive",
            CommandType.Text,
            MapTicket);

    public Task<List<Ticket>> GetOverdueAsync(string? departament = null, string? prioritate = null) =>
        _db.QueryAsync(
            "sp_GetTicheteIntarziate",
            CommandType.StoredProcedure,
            MapTicketSlim,
            ("@departament", (object?)departament),
            ("@prioritate", (object?)prioritate));

    public Task<List<Ticket>> GetCriticalAsync(int oreRaspunsMax = 4) =>
        _db.QueryAsync(
            "sp_GetTicheteCritice",
            CommandType.StoredProcedure,
            MapTicketSlim,
            ("@ore_raspuns_max", oreRaspunsMax));

    public async Task<int> OpenTicketAsync(
        string titlu, string? descriere, int clientId, int categorieId,
        string prioritate, string tip, int? tehnicianId, int? createdBy)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd  = new SqlCommand("sp_DeschideTichet", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@titlu", titlu);
        cmd.Parameters.AddWithValue("@descriere", (object?)descriere ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@client_id", clientId);
        cmd.Parameters.AddWithValue("@categorie_id", categorieId);
        cmd.Parameters.AddWithValue("@prioritate", prioritate);
        cmd.Parameters.AddWithValue("@tip", tip);
        cmd.Parameters.AddWithValue("@tehnician_id", (object?)tehnicianId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@creat_de", (object?)createdBy ?? DBNull.Value);
        var outId = new SqlParameter("@tichet_id_out", SqlDbType.Int) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(outId);
        await cmd.ExecuteNonQueryAsync();
        return outId.Value is int i ? i : 0;
    }

    public async Task CloseTicketAsync(int tichetId, string? note, int? rating, decimal? oreLucrate, int? inchisDe)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd  = new SqlCommand("sp_InchideTichet", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@tichet_id", tichetId);
        cmd.Parameters.AddWithValue("@note_inchidere", (object?)note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@rating_client", (object?)rating ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ore_lucrate", (object?)oreLucrate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@inchis_de", (object?)inchisDe ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    // Maperi separati pentru forma "full" (din vw_TicheteActive) vs forma "slim" returnata
    // de sp_GetTicheteCritice etc. Nu vrem sa explodam la GetOrdinal pe coloane lipsa.
    private static Ticket MapTicket(SqlDataReader r) =>
        new(
            TichetId:         r.GetInt32(r.GetOrdinal("tichet_id")),
            NumarTichet:      r.GetString(r.GetOrdinal("numar_tichet")),
            Titlu:            r.GetString(r.GetOrdinal("titlu")),
            Descriere:        null,
            Client:           r.GetString(r.GetOrdinal("client")),
            Categorie:        r.GetString(r.GetOrdinal("categorie")),
            Departament:      r.GetString(r.GetOrdinal("departament")),
            Prioritate:       r.GetString(r.GetOrdinal("prioritate")),
            Status:           r.GetString(r.GetOrdinal("status")),
            Tip:              r.GetString(r.GetOrdinal("tip")),
            Tehnician:        r.GetString(r.GetOrdinal("tehnician")),
            NivelTehnician:   DatabaseService.GetNullableString(r, "nivel_tehnician"),
            TipSla:           DatabaseService.GetNullableString(r, "tip_sla"),
            TimpRaspunsOre:   DatabaseService.GetNullable<int>(r, "timp_raspuns_ore"),
            TimpRezolvareOre: DatabaseService.GetNullable<int>(r, "timp_rezolvare_ore"),
            OreDeschis:       r.GetInt32(r.GetOrdinal("ore_deschis")),
            SlaDepasit:       r.GetInt32(r.GetOrdinal("sla_depasit")) == 1,
            DataDeschidere:   r.GetDateTime(r.GetOrdinal("data_deschidere")),
            DataRezolvare:    null,
            DataInchidere:    null,
            OreLucrate:       DatabaseService.GetNullable<decimal>(r, "ore_lucrate"),
            RatingClient:     null);

    private static Ticket MapTicketSlim(SqlDataReader r) =>
        new(
            TichetId:         0,
            NumarTichet:      r.GetString(r.GetOrdinal("numar_tichet")),
            Titlu:            r.GetString(r.GetOrdinal("titlu")),
            Descriere:        null,
            Client:           r.GetString(r.GetOrdinal("client")),
            Categorie:        r.GetString(r.GetOrdinal("categorie")),
            Departament:      ColumnExists(r, "departament") ? r.GetString(r.GetOrdinal("departament")) : "",
            Prioritate:       r.GetString(r.GetOrdinal("prioritate")),
            Status:           r.GetString(r.GetOrdinal("status")),
            Tip:              "",
            Tehnician:        r.GetString(r.GetOrdinal("tehnician")),
            NivelTehnician:   null,
            TipSla:           null,
            TimpRaspunsOre:   null,
            TimpRezolvareOre: ColumnExists(r, "sla_ore") ? DatabaseService.GetNullable<int>(r, "sla_ore") : null,
            OreDeschis:       r.GetInt32(r.GetOrdinal("ore_deschis")),
            SlaDepasit:       false,
            DataDeschidere:   r.GetDateTime(r.GetOrdinal("data_deschidere")),
            DataRezolvare:    null,
            DataInchidere:    null,
            OreLucrate:       null,
            RatingClient:     null);

    private static bool ColumnExists(SqlDataReader r, string name)
    {
        for (var i = 0; i < r.FieldCount; i++)
            if (string.Equals(r.GetName(i), name, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
