using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Services;

// CategoryRepository — folosit doar pe ecranul de creare tichet pentru dropdown.
// Returnam si numele departamentului pentru ca selectorul sa fie "(Network) Conectivitate VPN"
// — context-ul de departament ajuta cand un tehnician scaneaza lista de 10+ categorii.
public sealed class CategoryRepository
{
    private readonly DatabaseService _db;
    public CategoryRepository(DatabaseService db) => _db = db;

    public Task<List<CategoryOption>> GetAllAsync() =>
        _db.QueryAsync(
            @"SELECT c.categorie_id, c.nume_categorie, d.nume_departament
              FROM Categorii c
              JOIN Departamente d ON c.departament_id = d.departament_id
              ORDER BY d.nume_departament, c.nume_categorie",
            CommandType.Text,
            r => new CategoryOption(
                CategorieId:     r.GetInt32(r.GetOrdinal("categorie_id")),
                NumeCategorie:   r.GetString(r.GetOrdinal("nume_categorie")),
                NumeDepartament: r.GetString(r.GetOrdinal("nume_departament"))));
}
