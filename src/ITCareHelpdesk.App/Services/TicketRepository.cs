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

    // Tichete inchise — folosit ca knowledge base de catre AI pentru a gasi tichete similare
    // (cand userul creeaza un tichet nou, intrebam Claude care din astea inchise seamana cel mai bine).
    public Task<List<Ticket>> GetClosedAsync(int max = 50) =>
        _db.QueryAsync(
            $@"SELECT TOP ({max})
                t.tichet_id, t.numar_tichet, t.titlu, t.descriere,
                c.nume_companie AS client,
                cat.nume_categorie AS categorie,
                d.nume_departament AS departament,
                t.prioritate, t.status, t.tip,
                ISNULL(teh.prenume + N' ' + teh.nume, N'—') AS tehnician,
                NULL AS nivel_tehnician,
                NULL AS tip_sla,
                CAST(NULL AS INT) AS timp_raspuns_ore,
                CAST(NULL AS INT) AS timp_rezolvare_ore,
                ISNULL(DATEDIFF(HOUR, t.data_deschidere, t.data_rezolvare), 0) AS ore_deschis,
                0 AS sla_depasit,
                t.data_deschidere,
                t.ore_lucrate
              FROM Tichete t
              JOIN Clienti c          ON t.client_id    = c.client_id
              JOIN Categorii cat      ON t.categorie_id = cat.categorie_id
              JOIN Departamente d     ON cat.departament_id = d.departament_id
              LEFT JOIN Tehnicieni teh ON t.tehnician_id = teh.tehnician_id
              WHERE t.status IN (N'CLOSED', N'RESOLVED')
              ORDER BY t.data_inchidere DESC",
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

    // Citim un singur tichet cu descrierea completa (vw_TicheteActive nu o include — view-ul e optimizat
    // pentru lista). Aici facem un query direct ca sa luam toate detaliile pentru drawer.
    public async Task<Ticket?> GetByIdAsync(int tichetId)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(@"
            SELECT
                t.tichet_id, t.numar_tichet, t.titlu, t.descriere,
                c.nume_companie AS client,
                cat.nume_categorie AS categorie,
                d.nume_departament AS departament,
                t.prioritate, t.status, t.tip,
                ISNULL(teh.prenume + N' ' + teh.nume, N'—') AS tehnician,
                teh.nivel AS nivel_tehnician,
                sla.tip_sla, sla.timp_raspuns_ore, sla.timp_rezolvare_ore,
                DATEDIFF(HOUR, t.data_deschidere, GETDATE()) AS ore_deschis,
                CASE
                    WHEN sla.sla_id IS NULL THEN 0
                    WHEN t.data_rezolvare IS NOT NULL THEN 0
                    WHEN DATEDIFF(HOUR, t.data_deschidere, GETDATE()) > sla.timp_rezolvare_ore THEN 1
                    ELSE 0
                END AS sla_depasit,
                t.data_deschidere, t.data_rezolvare, t.data_inchidere,
                t.ore_lucrate, t.rating_client
            FROM Tichete t
            JOIN Clienti c          ON t.client_id    = c.client_id
            JOIN Categorii cat      ON t.categorie_id = cat.categorie_id
            JOIN Departamente d     ON cat.departament_id = d.departament_id
            LEFT JOIN Tehnicieni teh ON t.tehnician_id  = teh.tehnician_id
            LEFT JOIN ContracteSLA sla ON t.sla_id       = sla.sla_id
            WHERE t.tichet_id = @id", conn);
        cmd.Parameters.AddWithValue("@id", tichetId);

        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;

        return new Ticket(
            TichetId:         r.GetInt32(r.GetOrdinal("tichet_id")),
            NumarTichet:      r.GetString(r.GetOrdinal("numar_tichet")),
            Titlu:            r.GetString(r.GetOrdinal("titlu")),
            Descriere:        DatabaseService.GetNullableString(r, "descriere"),
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
            DataRezolvare:    DatabaseService.GetNullable<System.DateTime>(r, "data_rezolvare"),
            DataInchidere:    DatabaseService.GetNullable<System.DateTime>(r, "data_inchidere"),
            OreLucrate:       DatabaseService.GetNullable<decimal>(r, "ore_lucrate"),
            RatingClient:     DatabaseService.GetNullable<int>(r, "rating_client"));
    }

    // Adauga un comentariu in IstoricActivitate. Folosit din drawer.
    public async Task AddCommentAsync(int tichetId, string mesaj, int? userId)
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd = new SqlCommand(@"
            INSERT INTO IstoricActivitate (tichet_id, tip_activitate, mesaj, efectuat_de)
            VALUES (@tid, N'COMMENT', @msg, @uid)", conn);
        cmd.Parameters.AddWithValue("@tid", tichetId);
        cmd.Parameters.AddWithValue("@msg", mesaj);
        cmd.Parameters.AddWithValue("@uid", (object?)userId ?? System.DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

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
