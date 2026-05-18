using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// Tinem si Login si SignUp si OTP-Verify in acelasi VM, ca un mic "state machine".
// Alternativa cu 3 ViewModels separate ar duplica injectiile si ar complica tranzitia animata
// intre paneluri. Aici comutam doar un enum si fiecare panel observe lazy ce ii pasa.
public sealed partial class LoginViewModel : ViewModelBase
{
    public enum AuthPanel { SignIn, SignUp, OtpVerify }

    private readonly AuthService _auth;
    private readonly OtpService _otp;
    private readonly SessionService _session;
    private readonly ToastService _toast;

    [ObservableProperty] private AuthPanel _panel = AuthPanel.SignIn;

    // ===== SIGN-IN fields =====
    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string? _signInError;

    // ===== SIGN-UP fields =====
    [ObservableProperty] private string _newUsername = "";
    [ObservableProperty] private string _newFullName = "";
    [ObservableProperty] private string _newEmail = "";
    [ObservableProperty] private string _newPassword = "";
    [ObservableProperty] private string _confirmPassword = "";
    [ObservableProperty] private string? _signUpError;

    // ===== OTP fields =====
    // OtpDigits = 6 caractere; binding individual per casuta este idiomatic in Avalonia.
    [ObservableProperty] private string _otpD1 = "";
    [ObservableProperty] private string _otpD2 = "";
    [ObservableProperty] private string _otpD3 = "";
    [ObservableProperty] private string _otpD4 = "";
    [ObservableProperty] private string _otpD5 = "";
    [ObservableProperty] private string _otpD6 = "";
    [ObservableProperty] private string? _otpError;
    [ObservableProperty] private string _otpIdentifier = "";

    // Eveniment ascultat de Window pentru a face transitia la MainWindow dupa login.
    public event EventHandler<Models.AppUser>? SignedIn;

    public LoginViewModel(AuthService auth, OtpService otp, SessionService session, ToastService toast)
    {
        _auth = auth;
        _otp = otp;
        _session = session;
        _toast = toast;
    }

    // ============================================================
    // SIGN IN
    // ============================================================
    [RelayCommand]
    private async Task SignInAsync()
    {
        SignInError = null;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            SignInError = "Completeaza username si parola.";
            return;
        }

        IsBusy = true;
        BusyMessage = "Verificam datele...";
        try
        {
            var (result, user, msg) = await _auth.LoginAsync(Username.Trim(), Password);

            if (result == LoginResult.Success && user is not null)
            {
                _session.SignIn(user);
                _toast.ShowSuccess("Bun venit", $"Salut, {user.NumeComplet ?? user.Username}!");
                SignedIn?.Invoke(this, user);
                return;
            }

            // Pastram mesajul cat mai natural, fara expunerea de detalii interne ale DB-ului
            SignInError = result switch
            {
                LoginResult.InvalidCredentials => "Username sau parola gresita.",
                LoginResult.AccountInactive    => "Contul tau este inactiv. Contacteaza administratorul.",
                LoginResult.AccountLocked      => "Cont blocat din cauza incercarilor repetate. Reincearca peste cateva minute.",
                _ => msg
            };
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ============================================================
    // SIGN UP — pas 1: validare locala + cere OTP
    // ============================================================
    [RelayCommand]
    private async Task BeginSignUpAsync()
    {
        SignUpError = null;

        // Validari "ieftine" facute clientside ca sa nu aglomeram DB-ul cu cereri sigur-invalide
        if (NewUsername.Length < 3)            { SignUpError = "Username-ul are minim 3 caractere."; return; }
        if (NewFullName.Length < 3)            { SignUpError = "Introdu numele complet."; return; }
        if (!NewEmail.Contains('@'))           { SignUpError = "Email invalid."; return; }
        if (NewPassword.Length < 8)            { SignUpError = "Parola trebuie sa aiba minim 8 caractere."; return; }
        if (NewPassword != ConfirmPassword)    { SignUpError = "Parolele nu se potrivesc."; return; }

        IsBusy = true;
        BusyMessage = "Trimitem codul de verificare...";
        try
        {
            // OtpIdentifier este id-ul logic pe care il "pastram" intre paneluri.
            // Folosim email-ul, dar putem switch-ui pe phone fara sa atingem UI-ul.
            OtpIdentifier = NewEmail.Trim();
            await _otp.RequestCodeAsync(OtpIdentifier, purpose: "SIGNUP");

            // Resetam casutele si comutam panelul cu o tranzitie pe care View-ul o anima singur.
            OtpD1 = OtpD2 = OtpD3 = OtpD4 = OtpD5 = OtpD6 = "";
            Panel = AuthPanel.OtpVerify;
        }
        catch (Exception ex)
        {
            SignUpError = $"Nu am putut trimite codul: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ============================================================
    // SIGN UP — pas 2: verificare OTP + creare cont
    // ============================================================
    [RelayCommand]
    private async Task VerifyOtpAndCreateAccountAsync()
    {
        OtpError = null;
        var code = $"{OtpD1}{OtpD2}{OtpD3}{OtpD4}{OtpD5}{OtpD6}";
        if (code.Length != 6) { OtpError = "Codul are exact 6 cifre."; return; }

        IsBusy = true;
        BusyMessage = "Verificam codul...";
        try
        {
            var verified = await _otp.VerifyCodeAsync(OtpIdentifier, code, "SIGNUP");
            if (!verified)
            {
                OtpError = "Cod invalid sau expirat. Reincearca sau cere unul nou.";
                return;
            }

            BusyMessage = "Cream contul...";
            var (ok, msg, _) = await _auth.SignUpAsync(NewUsername.Trim(), NewPassword, NewFullName.Trim(), NewEmail.Trim());
            if (!ok)
            {
                OtpError = msg;
                return;
            }

            _toast.ShowSuccess("Cont creat", "Te-ai inregistrat cu succes. Acum poti intra in cont.");

            // Pre-completam username-ul ca user-ul sa fie doar la un click distanta
            Username = NewUsername.Trim();
            Password = "";
            ResetSignUpFields();
            Panel = AuthPanel.SignIn;
        }
        catch (Exception ex)
        {
            OtpError = $"Eroare: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // OTP "Trimite din nou"
    [RelayCommand]
    private async Task ResendOtpAsync()
    {
        if (string.IsNullOrWhiteSpace(OtpIdentifier)) return;
        try
        {
            await _otp.RequestCodeAsync(OtpIdentifier, "SIGNUP");
            _toast.ShowInfo("Cod retrimis", "Un cod nou a fost emis.");
        }
        catch (Exception ex)
        {
            OtpError = $"Eroare: {ex.Message}";
        }
    }

    // ============================================================
    // Comutari de panel
    // ============================================================
    [RelayCommand] private void GoToSignUp() { ResetSignUpFields(); Panel = AuthPanel.SignUp; }
    [RelayCommand] private void GoToSignIn() { Panel = AuthPanel.SignIn; }
    [RelayCommand] private void BackFromOtp() { Panel = AuthPanel.SignUp; }

    private void ResetSignUpFields()
    {
        NewUsername = NewFullName = NewEmail = NewPassword = ConfirmPassword = "";
        SignUpError = null;
    }
}
