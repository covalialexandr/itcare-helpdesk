using ITCareHelpdesk.App.Models;

namespace ITCareHelpdesk.App.Services;

// Tinem aici user-ul curent ca singleton in DI. ViewModels-urile primesc serviciul si
// pot citi sesiunea fara sa-l mai paseze de la window la window prin constructor.
// Trade-off: state global. Acceptabil pentru desktop single-user.
public sealed class SessionService
{
    public AppUser? CurrentUser { get; private set; }

    public bool IsAuthenticated => CurrentUser != null;
    public bool IsAdmin   => CurrentUser?.Role == "Admin";
    public bool IsManager => CurrentUser?.Role is "Admin" or "Manager";

    public void SignIn(AppUser user) => CurrentUser = user;
    public void SignOut() => CurrentUser = null;
}
