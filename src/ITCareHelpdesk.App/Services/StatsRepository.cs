using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Services;

// ============================================================
// StatsRepository
// ============================================================
// "Repository de agregari" — diferit de celelalte repository-uri care merg pe o tabela.
// Aici stau toate query-urile de tip statistic, calculate prin SUM/COUNT/AVG/GROUP BY.
// Folosit aproape exclusiv de DashboardViewModel pentru a popula KPI-urile si chart-urile.
//
// Metode disponibile:
//   GetKpiAsync               - un SINGUR query cu mai multe subqueries pentru cele 6 KPI principale.
//                               Optimizare deliberata: 6 metrici intr-un round-trip in loc de 6.
//   GetTechniciansAsync       - statistici per tehnician (sp_GetStatisticiTehnicieni)
//   GetTopTechniciansAsync    - top N tehnicieni dupa numarul de tichete rezolvate, cu loc_clasament
//   GetCategoryStatsAsync     - statistici per categorie (volum, procent rezolvat, timp mediu)
//   GetStatusDistributionAsync - pentru chart-ul donut: numar tichete per status
//   GetResolvedByDayAsync     - pentru sparkline: tichete rezolvate per zi pe ultimele N zile.
//                               Foloseste un CTE cu calendar generat ca sa avem si zilele "cu 0",
//                               altfel sparkline-ul ar avea gauri si trendline-ul ar fi confuz.
//
// Toate metodele sunt async ca apelantul (DashboardViewModel) sa poata rula cu Task.WhenAll
// — adica toate cererile in paralel, NU secvential.
// ============================================================
public sealed class StatsRepository
{
    private readonly DatabaseService _db;
    public StatsRepository(DatabaseService db) => _db = db;

    public Task<List<Technician>> GetTechniciansAsync() =>
        _db.QueryAsync(
            "sp_GetStatisticiTehnicieni",
            CommandType.StoredProcedure,
            r => new Technician(
                TehnicianId:            0,
                CodTehnician:           r.GetString(r.GetOrdinal("cod_tehnician")),
                Nume:                   r.GetString(r.GetOrdinal("tehnician")),
                Prenume:                "",
                Email:                  "",
                Specializare:           r.GetString(r.GetOrdinal("specializare")),
                Nivel:                  r.GetString(r.GetOrdinal("nivel")),
                TotalTichete:           r.GetInt32(r.GetOrdinal("total_tichete")),
                TicheteRezolvate:       r.GetInt32(r.GetOrdinal("tichete_rezolvate")),
                TicheteDeschise:        r.GetInt32(r.GetOrdinal("tichete_deschise")),
                TimpMediuRezolvareOre:  DatabaseService.GetNullable<decimal>(r, "timp_mediu_rezolvare_ore"),
                RatingMediu:            DatabaseService.GetNullable<decimal>(r, "rating_mediu"),
                LocClasament:           null));

    public Task<List<Technician>> GetTopTechniciansAsync(int topN = 10) =>
        _db.QueryAsync(
            "sp_GetTopTehnicieni",
            CommandType.StoredProcedure,
            r => new Technician(
                TehnicianId:            0,
                CodTehnician:           r.GetString(r.GetOrdinal("cod_tehnician")),
                Nume:                   r.GetString(r.GetOrdinal("tehnician")),
                Prenume:                "",
                Email:                  "",
                Specializare:           r.GetString(r.GetOrdinal("specializare")),
                Nivel:                  r.GetString(r.GetOrdinal("nivel")),
                TotalTichete:           0,
                TicheteRezolvate:       r.GetInt32(r.GetOrdinal("tichete_rezolvate")),
                TicheteDeschise:        r.GetInt32(r.GetOrdinal("tichete_deschise")),
                TimpMediuRezolvareOre:  DatabaseService.GetNullable<decimal>(r, "timp_mediu_rezolvare_ore"),
                RatingMediu:            DatabaseService.GetNullable<decimal>(r, "rating_mediu"),
                LocClasament:           (int)r.GetInt64(r.GetOrdinal("loc_clasament"))),
            ("@top_n", topN));

    public Task<List<CategoryStat>> GetCategoryStatsAsync() =>
        _db.QueryAsync(
            "sp_GetTimpiMediiPerCategorie",
            CommandType.StoredProcedure,
            r => new CategoryStat(
                Categorie:        r.GetString(r.GetOrdinal("categorie")),
                Departament:      r.GetString(r.GetOrdinal("departament")),
                TotalTichete:     r.GetInt32(r.GetOrdinal("total_tichete")),
                Rezolvate:        r.GetInt32(r.GetOrdinal("rezolvate")),
                ProcentRezolvate: DatabaseService.GetNullable<decimal>(r, "procent_rezolvate"),
                TimpMediuOre:     DatabaseService.GetNullable<decimal>(r, "timp_mediu_ore"),
                TotalOreLucrate:  DatabaseService.GetNullable<decimal>(r, "total_ore_lucrate")));

    public async Task<DashboardKpi> GetKpiAsync()
    {
        await using var conn = await _db.OpenConnectionAsync();

        // KPI-uri agregate intr-un singur round-trip — fiecare subquery costa,
        // dar ramane mai ieftin decat 6 trip-uri separate la baza.
        //
        // ATENTIE pe sla_compliance: SQL Server NU permite SUM() peste o expresie care contine
        // o subquery corelata. Anterior aveam:
        //    SUM(CASE WHEN ... <= ISNULL((SELECT ... FROM ContracteSLA s WHERE s.sla_id = t.sla_id)) ...)
        // si pica cu "Cannot perform an aggregate function on an expression containing an aggregate
        // or a subquery". Rezolvarea corecta este sa facem LEFT JOIN pe ContracteSLA in subselect-ul
        // de sla_compliance, asa expresia din SUM devine plata (fara subquery).
        var sql = @"
            SELECT
              (SELECT COUNT(*) FROM Tichete WHERE status IN (N'OPEN', N'IN_PROGRESS')) AS open_count,
              (SELECT COUNT(*) FROM vw_TicheteIntarziate) AS overdue,
              (SELECT COUNT(*) FROM Tichete
                WHERE status IN (N'RESOLVED', N'CLOSED')
                  AND CAST(data_rezolvare AS DATE) = CAST(GETDATE() AS DATE)) AS resolved_today,
              (SELECT COUNT(*) FROM Clienti WHERE activ = 1) AS active_clients,
              (SELECT AVG(CAST(rating_client AS DECIMAL(3,2))) FROM Tichete WHERE rating_client IS NOT NULL) AS avg_sat,
              (SELECT CAST(
                  SUM(CASE WHEN t.data_rezolvare IS NOT NULL
                            AND DATEDIFF(HOUR, t.data_deschidere, t.data_rezolvare) <= ISNULL(sla.timp_rezolvare_ore, 999999)
                            THEN 1 ELSE 0 END) * 100.0 /
                  NULLIF(SUM(CASE WHEN t.data_rezolvare IS NOT NULL THEN 1 ELSE 0 END), 0)
                  AS INT)
               FROM Tichete t
               LEFT JOIN ContracteSLA sla ON sla.sla_id = t.sla_id) AS sla_compliance";

        await using var cmd = new Microsoft.Data.SqlClient.SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new DashboardKpi(
                TicheteDeschise:      reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                TicheteIntarziate:    reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                TicheteRezolvateAzi:  reader.IsDBNull(2) ? 0 : reader.GetInt32(2),
                ClientiActivi:        reader.IsDBNull(3) ? 0 : reader.GetInt32(3),
                SatisfactieMedia:     reader.IsDBNull(4) ? 0 : reader.GetDecimal(4),
                SlaCompliancePercent: reader.IsDBNull(5) ? 0 : reader.GetInt32(5));
        }
        return new DashboardKpi(0, 0, 0, 0, 0, 0);
    }

    // Distributia tichetelor pe status — folosita pentru donut/bar chart pe Dashboard.
    // GROUP BY direct, fara SP, ca sa pastram lucrurile simple — query-ul e citit o data la fiecare deschidere de dashboard.
    public Task<List<StatusBucket>> GetStatusDistributionAsync() =>
        _db.QueryAsync(
            @"SELECT status, COUNT(*) AS nr
              FROM Tichete
              GROUP BY status",
            CommandType.Text,
            r => new StatusBucket(
                Status: r.GetString(0),
                Count:  r.GetInt32(1)));

    // Rezolvate per zi, ultimele N zile. Folosim CTE cu date generate ca sa avem si zilele "cu 0",
    // altfel chart-ul ar avea gauri si trendline-ul ar fi confuz.
    public Task<List<DailyResolved>> GetResolvedByDayAsync(int zile = 7) =>
        _db.QueryAsync(
            @"WITH zile_calendar AS (
                  SELECT CAST(DATEADD(DAY, -v.n + 1, GETDATE()) AS DATE) AS zi
                  FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9),(10),(11),(12),(13),(14)) AS v(n)
                  WHERE v.n <= @zile
              )
              SELECT
                  FORMAT(zc.zi, 'dd MMM', 'ro-RO') AS eticheta,
                  ISNULL(COUNT(t.tichet_id), 0)    AS nr
              FROM zile_calendar zc
              LEFT JOIN Tichete t
                  ON CAST(t.data_rezolvare AS DATE) = zc.zi
                 AND t.status IN (N'RESOLVED', N'CLOSED')
              GROUP BY zc.zi
              ORDER BY zc.zi ASC",
            CommandType.Text,
            r => new DailyResolved(
                Eticheta: r.GetString(0),
                Count:    r.GetInt32(1)),
            ("@zile", zile));
}
