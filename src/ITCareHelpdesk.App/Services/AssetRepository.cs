using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Services;

// ============================================================
// AssetRepository
// ============================================================
// Repository pentru tabela "Assets" — inventarul de echipamente IT al clientilor:
// laptopuri, servere, switch-uri, imprimante, firewalls etc.
//
// Repository-ul separa logica SQL de UI/ViewModel.
// Astfel VM-ul nu stie nimic despre SQL, conexiuni sau DataReader,
// el doar cere o lista de Asset-uri gata mapate.
//
// GetAllAsync foloseste JOIN cu tabela Clienti pentru a aduce direct
// numele companiei. Daca luam doar client_id ar mai trebui inca un query
// separat sau lookup in memorie.
//
// Avantaj:
//   * mai putine apeluri SQL
//   * cod mai simplu in View
//   * binding direct in DataGrid/XAML
//
// Query-ul este read-only. Nu avem INSERT/UPDATE/DELETE aici pentru ca
// asset-urile sunt momentan administrate manual din SQL Server.
//
// ORDER BY cod_asset pastreaza lista stabila si usor de parcurs.
// Fara ORDER BY SQL poate returna randurile in orice ordine.
//
// Repository-ul foloseste DatabaseService pentru executia efectiva.
// Astfel toata logica de conexiune si mapping helper ramane centralizata.
// ============================================================
public sealed class AssetRepository
{
    // Referinta catre serviciul principal de DB.
    // Injectarea prin constructor permite Dependency Injection
    // si testare mai usoara.
    private readonly DatabaseService _db;

    // Constructor simplu.
    // Primim DatabaseService din container-ul de DI.
    public AssetRepository(DatabaseService db) => _db = db;

    // ========================================================
    // GetAllAsync
    // ========================================================
    // Returneaza toate asset-urile existente in sistem.
    //
    // Async pentru ca apelul SQL poate dura:
    //   * retea lenta
    //   * multe randuri
    //   * SQL Server ocupat
    //
    // Fara async UI-ul Avalonia ar ingheta pana vine raspunsul.
    // ========================================================
    public Task<List<Asset>> GetAllAsync() =>
        _db.QueryAsync(
            @"SELECT a.asset_id, a.cod_asset, a.denumire, a.tip, a.producator, a.model,
                     c.nume_companie AS client, a.locatie, a.status, a.garantie_pana
              FROM Assets a
              JOIN Clienti c ON a.client_id = c.client_id
              ORDER BY a.cod_asset",

            // CommandType.Text = query SQL normal.
            // Alternativa ar fi StoredProcedure.
            CommandType.Text,

            // Mapper-ul transforma fiecare rand SQL intr-un obiect Asset.
            // Practic facem manual ce face un ORM precum Dapper/EF.
            //
            // Avantaj:
            //   * control complet
            //   * foarte rapid
            //   * zero magie ascunsa
            //
            // Dezavantaj:
            //   * mai mult cod manual
            r => new Asset(

                // asset_id este cheia primara.
                // GetOrdinal cauta indexul coloanei dupa nume.
                // Este mai sigur decat hardcodarea indexului numeric.
                AssetId:       r.GetInt32(r.GetOrdinal("asset_id")),

                // Cod unic asset ex: LAP-001
                CodAsset:      r.GetString(r.GetOrdinal("cod_asset")),

                // Denumirea afisata in UI.
                Denumire:      r.GetString(r.GetOrdinal("denumire")),

                // Tipul echipamentului:
                // laptop, server, switch etc.
                Tip:           r.GetString(r.GetOrdinal("tip")),
                // Campurile nullable NU pot folosi direct GetString.
                // Daca valoarea este NULL in SQL ar crapa cu exceptie.
                //
                // Helper-ul GetNullableString verifica DBNull automat.
                Producator:    DatabaseService.GetNullableString(r, "producator"),
                // Model hardware optional.
                Model:         DatabaseService.GetNullableString(r, "model"),
                // Alias-ul "client" vine din:
                // c.nume_companie AS client
                //
                // Astfel modelul Asset primeste direct numele clientului.
                Client:        r.GetString(r.GetOrdinal("client")),
                // Unele asset-uri nu au locatie setata.
                Locatie:       DatabaseService.GetNullableString(r, "locatie"),
                // Status operational:
                // ACTIV, DEFECT, IN_SERVICE etc.
                Status:        r.GetString(r.GetOrdinal("status")),
                // Garantie optionala.
                // Generic helper pentru nullable types.
                GarantiePana:  DatabaseService.GetNullable<System.DateTime>(r, "garantie_pana")));
}