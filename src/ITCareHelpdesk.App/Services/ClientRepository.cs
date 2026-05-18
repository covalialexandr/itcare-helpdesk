using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Services;

public sealed class ClientRepository
{
    private readonly DatabaseService _db;
    public ClientRepository(DatabaseService db) => _db = db;

    public Task<List<Client>> GetAllAsync() =>
        _db.QueryAsync(
            @"SELECT c.client_id, c.nume_companie, c.industrie, c.oras, c.telefon, c.email_contact,
                     c.data_contract, c.activ,
                     (SELECT COUNT(*) FROM Tichete t WHERE t.client_id = c.client_id) AS nr_tichete
              FROM Clienti c
              WHERE c.activ = 1
              ORDER BY c.nume_companie",
            CommandType.Text,
            r => new Client(
                ClientId:     r.GetInt32(r.GetOrdinal("client_id")),
                NumeCompanie: r.GetString(r.GetOrdinal("nume_companie")),
                Industrie:    DatabaseService.GetNullableString(r, "industrie"),
                Oras:         DatabaseService.GetNullableString(r, "oras"),
                Telefon:      DatabaseService.GetNullableString(r, "telefon"),
                EmailContact: DatabaseService.GetNullableString(r, "email_contact"),
                DataContract: r.GetDateTime(r.GetOrdinal("data_contract")),
                Activ:        r.GetBoolean(r.GetOrdinal("activ")),
                NrTichete:    r.GetInt32(r.GetOrdinal("nr_tichete"))));
}
