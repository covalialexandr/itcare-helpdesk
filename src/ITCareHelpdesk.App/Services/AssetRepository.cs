using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Services;

public sealed class AssetRepository
{
    private readonly DatabaseService _db;
    public AssetRepository(DatabaseService db) => _db = db;

    public Task<List<Asset>> GetAllAsync() =>
        _db.QueryAsync(
            @"SELECT a.asset_id, a.cod_asset, a.denumire, a.tip, a.producator, a.model,
                     c.nume_companie AS client, a.locatie, a.status, a.garantie_pana
              FROM Assets a
              JOIN Clienti c ON a.client_id = c.client_id
              ORDER BY a.cod_asset",
            CommandType.Text,
            r => new Asset(
                AssetId:       r.GetInt32(r.GetOrdinal("asset_id")),
                CodAsset:      r.GetString(r.GetOrdinal("cod_asset")),
                Denumire:      r.GetString(r.GetOrdinal("denumire")),
                Tip:           r.GetString(r.GetOrdinal("tip")),
                Producator:    DatabaseService.GetNullableString(r, "producator"),
                Model:         DatabaseService.GetNullableString(r, "model"),
                Client:        r.GetString(r.GetOrdinal("client")),
                Locatie:       DatabaseService.GetNullableString(r, "locatie"),
                Status:        r.GetString(r.GetOrdinal("status")),
                GarantiePana:  DatabaseService.GetNullable<System.DateTime>(r, "garantie_pana")));
}
