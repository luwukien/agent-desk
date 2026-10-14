using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AgentDesk.Spike;

public partial class MainWindow : Window
{
    private readonly List<TerminalSession> _sessions = new();
    private int _nextNumber;
    private bool _closing;
    private bool _allowClose;
    public TerminalSession? Selected => (Tabs.SelectedItem as TabItem)?.Tag as TerminalSession;

    public MainWindow()
    {
        InitializeComponent();
        FolderBox.Text = Environment.CurrentDirectory;
        Loaded += (_, _) => AddSession();
    }

    public TerminalSession AddSession(string? command = null)
    {
        var session = new TerminalSession($"PowerShell {++_nextNumber}", FolderBox.Text, command);
        session.StateChanged += OnSessionStateChanged;
        _sessions.Add(session);
        TerminalHost.Children.Add(session.View);
        var tab = new TabItem { Header = session.Name, Tag = session };
        Tabs.Items.Add(tab);
        Tabs.SelectedItem = tab;
        return session;
    }

    public void SelectSession(TerminalSession session)
    {
        Tabs.SelectedItem = Tabs.Items.Cast<TabItem>().Single(t => t.Tag == session);
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        foreach (var session in _sessions)
            session.View.Visibility = session == Selected ? Visibility.Visible : Visibility.Hidden;
        UpdateStatus();
        Dispatcher.BeginInvoke(() => Selected?.View.Focus());
    }

    private void OnSessionStateChanged(object? sender, EventArgs e) => UpdateStatus();
    private void UpdateStatus() => StatusText.Text = Selected is { } s
        ? $"{s.Name} · {s.Status} · {s.DirectoryPath} | Copy: Ctrl+Shift+C · Paste: Ctrl+Shift+V"
        : "Chưa có terminal. Bấm + PowerShell.";

    private void OnAdd(object sender, RoutedEventArgs e) => WithError(() => AddSession());
    private void OnCopy(object sender, RoutedEventArgs e) => WithError(() => Selected?.CopySelection());
    private void OnPaste(object sender, RoutedEventArgs e) => WithError(() => Selected?.Paste());
    private void OnInterrupt(object sender, RoutedEventArgs e) => WithError(() => Selected?.SendText("\x03"));

    private void WithError(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "AgentDesk"); }
        Selected?.View.Focus();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Selected?.View.IsKeyboardFocusWithin != true) return;
        if (Keyboard.Modifiers != (ModifierKeys.Control | ModifierKeys.Shift)) return;
        if (e.Key == Key.C) { e.Handled = true; OnCopy(sender, e); }
        if (e.Key == Key.V) { e.Handled = true; OnPaste(sender, e); }
    }

    private async void OnCloseTab(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } session || _closing) return;
        if (MessageBox.Show(this, "Đóng tab sẽ dừng shell và tiến trình con của tab này. Tiếp tục?",
            "Đóng terminal", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _closing = true;
        try { await RemoveSessionAsync(session); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không đóng được terminal"); }
        finally { _closing = false; }
    }

    public async Task RemoveSessionAsync(TerminalSession session)
    {
        await session.CloseAsync();
        session.StateChanged -= OnSessionStateChanged;
        _sessions.Remove(session);
        TerminalHost.Children.Remove(session.View);
        var tab = Tabs.Items.Cast<TabItem>().Single(t => t.Tag == session);
        Tabs.Items.Remove(tab);
        if (Tabs.SelectedIndex < 0 && Tabs.Items.Count > 0) Tabs.SelectedIndex = 0;
        UpdateStatus();
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        if (_closing) return;
        if (_sessions.Count > 0 && MessageBox.Show(this,
            "Thoát sẽ dừng tất cả terminal. Không lưu tiến trình hay cuộc hội thoại. Tiếp tục?",
            "Thoát AgentDesk", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        _closing = true;
        IsEnabled = false;
        try
        {
            foreach (var session in _sessions.ToArray()) await RemoveSessionAsync(session);
            _allowClose = true;
            Close();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Không đóng được terminal"); }
        finally { IsEnabled = true; _closing = false; }
    }

    internal async Task FinishSmokeAsync()
    {
        _allowClose = true;
        foreach (var session in _sessions.ToArray()) await RemoveSessionAsync(session);
        Close();
    }
}
