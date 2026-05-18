using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ITCareHelpdesk.App.Services;

// DatabaseService = wrapper subtire peste Microsoft.Data.SqlClient.
// L-am scris manual (in loc de Dapper sau EF) pentru ca:
// 1) practica de scoala cere sa demonstram ADO.NET / proceduri stocate explicite
// 2) e zero overhead — perfect pentru aplicatie desktop care nu serveste 10k QPS
// 3) e clar la review-uri ce SQL se executa, fara magie ORM ascunsa
public sealed class
    DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("ITCareHelpdesk")
            ?? throw new InvalidOperationException(
                "ConnectionString 'ITCareHelpdesk' nu este definit in appsettings.json");
    }

    // Expunem connection string-ul read-only pentru servicii care au nevoie de
    // SqlConnection direct (de ex. transactii complexe).
    public string ConnectionString => _connectionString;

    public async Task<SqlConnection> OpenConnectionAsync()
    {
        var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    // Helper generic care executa o procedura/query si materializeaza rezultatele
    // intr-o lista folosind un mapper. Ne salveaza foarte mult cod boilerplate.
    public async Task<List<T>> QueryAsync<T>(
        string sql,
        CommandType commandType,
        Func<SqlDataReader, T> map,
        params (string name, object? value)[] parameters)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd  = new SqlCommand(sql, conn) { CommandType = commandType };

        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        var results = new List<T>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(map(reader));

        return results;
    }

    // Executa o procedura/query care returneaza un singur scalar.
    public async Task<T?> ScalarAsync<T>(
        string sql,
        CommandType commandType,
        params (string name, object? value)[] parameters)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd  = new SqlCommand(sql, conn) { CommandType = commandType };

        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        var result = await cmd.ExecuteScalarAsync();
        return result is null or DBNull ? default : (T)Convert.ChangeType(result, typeof(T));
    }

    public async Task<int> ExecuteAsync(
        string sql,
        CommandType commandType,
        params (string name, object? value)[] parameters)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd  = new SqlCommand(sql, conn) { CommandType = commandType };

        foreach (var (name, value) in parameters)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);

        return await cmd.ExecuteNonQueryAsync();
    }

    // Verificare conexiune folosita in splash. Returnam si motivul real al esecului ca debug-ul
    // sa nu fie ghicit — multe erori SQL ascund cauza in InnerException.
    public async Task<(bool Ok, string? Error)> TestConnectionAsync()
    {
        try
        {
            await using var conn = await OpenConnectionAsync();
            return (conn.State == ConnectionState.Open, null);
        }
        catch (Exception ex)
        {
            var detail = ex.InnerException is { } inner ? $"{ex.Message} ({inner.Message})" : ex.Message;
            return (false, detail);
        }
    }

    // Pentru debug — expunem connection string-ul citit din config ca splash-ul sa-l afiseze
    // (parola eventuala e mascata pe acelasi rand).
    public string ConnectionStringForDisplay
    {
        get
        {
            // Mascam Password=... daca exista (pentru SQL auth); Trusted_Connection ramane vizibil.
            var s = _connectionString;
            var pwIdx = s.IndexOf("Password=", StringComparison.OrdinalIgnoreCase);
            if (pwIdx < 0) return s;
            var endIdx = s.IndexOf(';', pwIdx);
            return s.Substring(0, pwIdx) + "Password=****" + (endIdx > 0 ? s.Substring(endIdx) : "");
        }
    }

    // Helper extension pentru reader — citeste o coloana nullable fara sa explodeze pe DBNull
    public static T? GetNullable<T>(SqlDataReader r, string column) where T : struct
    {
        var idx = r.GetOrdinal(column);
        return r.IsDBNull(idx) ? null : (T)r.GetValue(idx);
    }

    public static string? GetNullableString(SqlDataReader r, string column)
    {
        var idx = r.GetOrdinal(column);
        return r.IsDBNull(idx) ? null : r.GetString(idx);
    }
}
