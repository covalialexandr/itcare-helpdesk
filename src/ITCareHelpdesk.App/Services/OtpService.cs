using System;
using System.Data;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ITCareHelpdesk.App.Services;

// Simulam un flow real de OTP (One-Time Password) fara sa atingem un provider SMS/email.
// Codul este salvat in DB cu expirare; UI-ul il afiseaza printr-un toast ca "debug helper".
// Avantajul abordarii: cod productie-ready in DB (poate fi inlocuit usor cu Twilio/MailKit
// schimband doar SendCodeAsync), zero dependente externe pentru prezentare.
public sealed class OtpService
{
    private readonly DatabaseService _db;
    private readonly bool   _simulationMode;
    private readonly int    _expirationMinutes;
    private readonly ToastService _toast;

    public OtpService(DatabaseService db, IConfiguration config, ToastService toast)
    {
        _db = db;
        _toast = toast;

        // Citim ca string și convertim manual (oferind valori default dacă sunt null)
        var simModeRaw = config["App:OtpSimulationMode"];
        _simulationMode = simModeRaw == null ? true : bool.Parse(simModeRaw);

        var expMinRaw = config["App:OtpExpirationMinutes"];
        _expirationMinutes = expMinRaw == null ? 5 : int.Parse(expMinRaw);
    }

    public bool SimulationMode => _simulationMode;

    public string GenerateCode()
    {
        // RandomNumberGenerator este criptografic-safe; Math.Random e predictibil si nu trebuie
        // folosit niciodata pentru tokenuri de securitate.
        Span<byte> buffer = stackalloc byte[4];
        RandomNumberGenerator.Fill(buffer);
        var number = BitConverter.ToUInt32(buffer) % 1_000_000;
        return number.ToString("D6");
    }

    public async Task<string> RequestCodeAsync(string identifier, string purpose = "SIGNUP")
    {
        var code = GenerateCode();
        var expiresAt = DateTime.UtcNow.AddMinutes(_expirationMinutes);

        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd  = new SqlCommand("sp_RequestOtp", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@identifier", identifier);
        cmd.Parameters.AddWithValue("@cod", code);
        cmd.Parameters.AddWithValue("@scop", purpose);
        cmd.Parameters.AddWithValue("@expira_la", expiresAt);
        await cmd.ExecuteNonQueryAsync();

        // In productie aici trimitem prin Twilio/SMTP. In modul simulare il aratam in toast
        // ca user-ul sa-l vada pentru testare.
        if (_simulationMode)
        {
            _toast.ShowOtp(code, identifier);
        }

        return code; // returnat doar pentru ca testele/integrarile sa-l aiba; UI-ul nu il foloseste
    }

    public async Task<bool> VerifyCodeAsync(string identifier, string code, string purpose = "SIGNUP")
    {
        await using var conn = await _db.OpenConnectionAsync();
        await using var cmd  = new SqlCommand("sp_VerifyOtp", conn) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@identifier", identifier);
        cmd.Parameters.AddWithValue("@cod", code);
        cmd.Parameters.AddWithValue("@scop", purpose);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return reader.GetInt32(0) == 1;
        return false;
    }
}
