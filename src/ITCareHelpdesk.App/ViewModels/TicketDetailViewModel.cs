using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ITCareHelpdesk.App.Models;
using ITCareHelpdesk.App.Services;

namespace ITCareHelpdesk.App.ViewModels;

// TicketDetailViewModel — alimenteaza drawer-ul cu detalii + istoric + actiuni.
// Drawer-ul nu este o fereastra separata (ar fi confuz vizual); este un panou suprapus peste
// TicketsView, controlat din TicketsViewModel.SelectedForDetail.
public sealed partial class TicketDetailViewModel : ViewModelBase
{
    private readonly TicketRepository _tickets;
    private readonly HistoryRepository _history;
    private readonly SessionService _session;
    private readonly ToastService _toast;
    private readonly AiSuggestionService _ai;

    public bool AiEnabled => _ai.IsConfigured;

    [ObservableProperty] private Ticket? _ticket;
    [ObservableProperty] private string _newComment = "";
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string? _aiSummary;

    public ObservableCollection<HistoryEntry> History { get; } = new();

    // Notifica TicketsView ca s-a inchis sau a aparut un eveniment care cere refresh la lista.
    public event EventHandler? CloseRequested;
    public event EventHandler? TicketChanged;

    public TicketDetailViewModel(
        TicketRepository tickets,
        HistoryRepository history,
        SessionService session,
        ToastService toast,
        AiSuggestionService ai)
    {
        _tickets = tickets;
        _history = history;
        _session = session;
        _toast = toast;
        _ai = ai;
    }

    [RelayCommand]
    private async Task SummarizeAsync()
    {
        if (Ticket is null) return;
        if (!_ai.IsConfigured)
        {
            _toast.ShowWarning("AI", "Provider-ul AI nu este configurat (vezi Ai:Provider in appsettings.json).");
            return;
        }

        IsBusy = true;
        BusyMessage = "Cerem rezumat AI...";
        try
        {
            AiSummary = await _ai.SummarizeHistoryAsync(Ticket.NumarTichet, Ticket.Titlu, History);
            _toast.ShowSuccess("Rezumat AI", "Generat. Vezi panoul de mai sus.");
        }
        catch (Exception ex)
        {
            _toast.ShowError("AI summary", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // Apelat cand utilizatorul da click pe un rand din lista de tichete.
    public async Task OpenAsync(int ticketId)
    {
        IsOpen = true;
        IsBusy = true;
        BusyMessage = "Aducem detaliile...";
        AiSummary = null;  // reset summary la fiecare deschidere
        try
        {
            // Paralelizam fetch-ul ticket + history
            var tt = _tickets.GetByIdAsync(ticketId);
            var ht = _history.GetForTicketAsync(ticketId);
            await Task.WhenAll(tt, ht);

            Ticket = await tt;
            History.Clear();
            foreach (var h in await ht) History.Add(h);
            NewComment = "";
        }
        catch (Exception ex)
        {
            _toast.ShowError("Detalii tichet", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        IsOpen = false;
        Ticket = null;
        History.Clear();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task AddCommentAsync()
    {
        if (Ticket is null) return;
        var msg = NewComment?.Trim();
        if (string.IsNullOrWhiteSpace(msg))
        {
            _toast.ShowWarning("Comentariu", "Scrie ceva inainte sa adaugi.");
            return;
        }

        IsBusy = true;
        BusyMessage = "Adaugam comentariul...";
        try
        {
            await _tickets.AddCommentAsync(Ticket.TichetId, msg, _session.CurrentUser?.UserId);
            _toast.ShowSuccess("Comentariu adaugat", "Salvat in istoric.");
            NewComment = "";
            // Reincarcam istoricul ca sa apara noul comentariu
            await OpenAsync(Ticket.TichetId);
            TicketChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _toast.ShowError("Eroare", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CloseTicketAsync()
    {
        if (Ticket is null) return;
        IsBusy = true;
        BusyMessage = "Inchidem tichetul...";
        try
        {
            await _tickets.CloseTicketAsync(
                tichetId: Ticket.TichetId,
                note: string.IsNullOrWhiteSpace(NewComment) ? "Inchis din detail drawer." : NewComment.Trim(),
                rating: null,
                oreLucrate: Ticket.OreLucrate,
                inchisDe: _session.CurrentUser?.UserId);

            _toast.ShowSuccess("Tichet inchis", $"{Ticket.NumarTichet} a fost inchis.");
            TicketChanged?.Invoke(this, EventArgs.Empty);
            Close();
        }
        catch (Exception ex)
        {
            _toast.ShowError("Eroare", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
