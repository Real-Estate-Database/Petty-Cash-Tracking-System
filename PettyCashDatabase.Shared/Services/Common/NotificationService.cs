using Microsoft.JSInterop;

namespace PettyCashDatabase.Shared.Services;

public enum AppNotificationKind
{
    Success,
    Error,
    Warning,
    Info
}

public sealed record AppNotification(
    Guid Id,
    AppNotificationKind Kind,
    string Title,
    string Message);

public interface INotificationService
{
    IReadOnlyList<AppNotification> Active { get; }

    event EventHandler? Changed;

    Task ShowAsync(
        AppNotificationKind kind,
        string title,
        string message,
        TimeSpan? duration = null);

    void Dismiss(Guid id);
}

/// <summary>
/// Circuit/device-scoped notifications shared by all components in the current UI.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IJSRuntime _js;
    private readonly List<AppNotification> _active = [];

    public NotificationService(IJSRuntime js) => _js = js;

    public IReadOnlyList<AppNotification> Active => _active;

    public event EventHandler? Changed;

    public async Task ShowAsync(
        AppNotificationKind kind,
        string title,
        string message,
        TimeSpan? duration = null)
    {
        var notification = new AppNotification(Guid.NewGuid(), kind, title, message);
        _active.Add(notification);
        Changed?.Invoke(this, EventArgs.Empty);

        try
        {
            await _js.InvokeVoidAsync("pcNotifications.play", kind.ToString().ToLowerInvariant());
        }
        catch (JSDisconnectedException)
        {
            // A navigation or circuit shutdown should never turn a notification
            // into an application error.
        }
        catch (JSException)
        {
            // If the optional sound script is unavailable, keep the visual toast.
        }
        catch (InvalidOperationException)
        {
            // JS interop can be unavailable during a non-interactive render.
        }

        _ = RemoveAfterDelayAsync(notification.Id, duration ?? TimeSpan.FromSeconds(5));
    }

    public void Dismiss(Guid id)
    {
        if (_active.RemoveAll(notification => notification.Id == id) > 0)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task RemoveAfterDelayAsync(Guid id, TimeSpan duration)
    {
        await Task.Delay(duration);
        Dismiss(id);
    }
}
