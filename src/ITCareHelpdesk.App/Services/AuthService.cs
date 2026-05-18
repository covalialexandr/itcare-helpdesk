using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using ITCareHelpdesk.App.Models;
using Microsoft.Data.SqlClient;

namespace ITCareHelpdesk.App.Services;

public enum LoginResult
{
    Success,
    InvalidCredentials,
    AccountInactive,
    AccountLocked,
    DatabaseError
}

public sealed class AuthService
{
    private readonly DatabaseService _db;

    // Hash-ul SHA256 simplu este OK pentru un proiect de practica, dar trebuie clar
    // documentat ca in productie reala s-ar folosi bcrypt/argon2 cu salt.
    // De ce SHA256 aici: matchuieste cu hash-urile deja salvate in DB-ul tau seed.
    public AuthService(DatabaseService db) => _db = db;

    public static string HashPassword(string plain)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plain));
        // Lower-case hex pentru a fi consistent cu hash-urile din seed-ul SQL
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public async Task<(LoginResult Result, AppUser? User, string Message)> LoginAsync(string username, string password)
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync();
            await using var cmd  = new SqlCommand("sp_Login", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@parola_hash", HashPassword(password));

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return (LoginResult.InvalidCredentials, null, "Username sau parola incorectă.");

            var succes = reader.GetInt32(0) == 1;
            var mesaj  = reader.IsDBNull(3) ? "" : reader.GetString(3);

            if (!succes)
                return (LoginResult.InvalidCredentials, null, mesaj);

            var userId = reader.GetInt32(1);
            var role   = reader.GetString(2);
            reader.Close();

            // Citim restul detaliilor user-ului din tabela direct
            await using var cmd2 = new SqlCommand(
                "SELECT user_id, username, rol, tehnician_id, nume_complet, email, data_creare " +
                "FROM Utilizatori WHERE user_id = @id", conn);
            cmd2.Parameters.AddWithValue("@id", userId);
            await using var r2 = await cmd2.ExecuteReaderAsync();

            if (await r2.ReadAsync())
            {
                var user = new AppUser(
                    UserId:      r2.GetInt32(0),
                    Username:    r2.GetString(1),
                    Role:        r2.GetString(2),
                    TehnicianId: r2.IsDBNull(3) ? null : r2.GetInt32(3),
                    NumeComplet: r2.IsDBNull(4) ? null : r2.GetString(4),
                    Email:       r2.IsDBNull(5) ? null : r2.GetString(5),
                    DataCreare:  r2.GetDateTime(6));

                return (LoginResult.Success, user, mesaj);
            }

            return (LoginResult.DatabaseError, null, "Eroare neașteptată la încărcarea profilului.");
        }
        catch (SqlException ex)
        {
            return (LoginResult.DatabaseError, null, $"Eroare DB: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message, int? UserId)> SignUpAsync(
        string username,
        string password,
        string fullName,
        string email,
        string role = "Technician")
    {
        try
        {
            await using var conn = await _db.OpenConnectionAsync();
            await using var cmd  = new SqlCommand("sp_SignUp", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@username", username);
            cmd.Parameters.AddWithValue("@parola_hash", HashPassword(password));
            cmd.Parameters.AddWithValue("@nume_complet", fullName);
            cmd.Parameters.AddWithValue("@email", email);
            cmd.Parameters.AddWithValue("@rol", role);
            var outId = new SqlParameter("@user_id_out", SqlDbType.Int) { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(outId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var ok = reader.GetInt32(0) == 1;
                var msg = reader.GetString(1);
                reader.Close();
                return (ok, msg, ok && outId.Value is int i ? i : null);
            }
            return (false, "Răspuns necunoscut de la server.", null);
        }
        catch (SqlException ex)
        {
            // Coduri SQL pentru constrangeri unice — daca username/email exista, dam mesaj prietenos
            if (ex.Number is 2627 or 2601)
                return (false, "Username sau email deja folosit.", null);
            return (false, $"Eroare DB: {ex.Message}", null);
        }
    }
}
