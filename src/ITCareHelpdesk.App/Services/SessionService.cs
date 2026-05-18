using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Services;

// ============================================================
// SessionService
// ============================================================
// Tine in memorie userul autentificat in sesiunea curenta. Inregistrat ca SINGLETON in DI,
// deci o singura instanta este partajata in toata aplicatia — orice ViewModel poate citi
// "cine este user-ul curent" fara sa-l mai paseze de la fereastra la fereastra prin constructor.
//
// Trade-off explicit: foloseste state global. In aplicatii web/multi-user asta ar fi o problema,
// dar pentru o aplicatie desktop single-user (un singur om in fata calculatorului la un moment),
// e abordarea cea mai simpla si pragmatica.
//
// Metode:
//   SignIn(user)   - apelat dupa autentificare reusita din LoginViewModel
//   SignOut()      - apelat la logout, goleste CurrentUser
//
// Proprietati:
//   CurrentUser    - obiectul AppUser curent (sau null daca nu e logat)
//   IsAuthenticated - shortcut bool
//   IsAdmin        - rol "Admin"
//   IsManager      - rol "Admin" sau "Manager"  (pentru meniuri/permisiuni gated)
// ============================================================
public sealed class SessionService
{
    public AppUser? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser != null;
    public bool IsAdmin   => CurrentUser?.Role == "Admin";
    public bool IsManager => CurrentUser?.Role is "Admin" or "Manager";

    public void SignIn(AppUser user) => CurrentUser = user;
    public void SignOut() => CurrentUser = null;
}
