using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using ITCareHelpdesk.App.ViewModels;

namespace ITCareHelpdesk.App.Views;

public partial class CreateTicketWindow : Window
{
    // Returnam un int? prin dialog: id-ul tichetului creat sau null daca s-a anulat.
    // Tinut simplu — fara TaskCompletionSource manual, doar property + Close().
    public int? CreatedTicketId { get; private set; }

    public CreateTicketWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (DataContext is CreateTicketViewModel vm)
                vm.TicketCreated += OnTicketCreated;
        };
    }

    private void OnTicketCreated(object? sender, int ticketId)
    {
        // -1 = anulare; orice altceva = id real al tichetului creat
        CreatedTicketId = ticketId > 0 ? ticketId : null;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        CreatedTicketId = null;
        Close();
    }

    private void OnDragChromeAreaPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
