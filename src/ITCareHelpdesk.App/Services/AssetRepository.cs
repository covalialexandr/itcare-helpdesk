using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Services;

// ============================================================
// AssetRepository
// ============================================================
// Repository pentru tabela "Assets" — inventarul de echipamente IT al clientilor:
// laptopuri, servere, switch-uri, imprimante, firewalls, etc.
//
// GetAllAsync include un JOIN cu tabela Clienti pentru a returna direct numele clientului
// (NU doar client_id). Aceasta denormalizare la nivel de query salveaza un al doilea
// SELECT din C# si simplifica template-ul din XAML — randul din tabel poate afisa
// "Aurora Industries" direct, fara binding indirect.
//
// Pagina Asset-uri (AssetsView) consuma aceasta lista, filtreaza pe tip echipament
// si pe text liber. Nu avem CRUD activ inca — assets-urile se introduc din SQL seed
// sau de la administrator prin SSMS. Pe viitor poate intra editor in UI.
// ============================================================
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
