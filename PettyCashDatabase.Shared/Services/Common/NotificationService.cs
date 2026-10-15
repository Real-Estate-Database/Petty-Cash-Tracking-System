using Microsoft.JSInterop;

namespace PettyCashDatabase.Shared.Services;

/// <summary>
/// Defines categories for application-wide user notifications and alerts.
/// </summary>
public enum AppNotificationKind
{
    Success,
    Error,
    Warning,
    Info
}

/// <summary>
/// Immutable record representing an active visual notification item in the toast stack.
/// </summary>
public sealed record AppNotification(
    Guid Id,
    AppNotificationKind Kind,
    string Title,
    string Message);

/// <summary>
/// Contract for circuit-scoped or device-scoped toast notifications.
/// </summary>
public interface INotificationService
{
    IReadOnlyList<AppNotification> Active { get; }

    event EventHandler? Changed;

    /// <summary>
    /// Displays a toast notification on the client interface.
    /// By default:
    /// - <see cref="AppNotificationKind.Error"/> and <see cref="AppNotificationKind.Warning"/> stick indefinitely until dismissed.
    /// - <see cref="AppNotificationKind.Success"/> and <see cref="AppNotificationKind.Info"/> persist for a generous readable duration.
    /// </summary>
    Task ShowAsync(
        AppNotificationKind kind,
        string title,
        string message,
        TimeSpan? duration = null);

    /// <summary>
    /// Dismisses a notification by its unique identifier.
    /// </summary>
    void Dismiss(Guid id);
}

/// <summary>
/// Circuit/device-scoped notifications shared by all components in the current UI.
/// Designed for user readability with sticky error/warning alerts and explicit dismissal controls.
/// </summary>
public sealed class NotificationService : INotificationService
{
    private readonly IJSRuntime _js;
    private readonly List<AppNotification> _active = [];

    public NotificationService(IJSRuntime js) => _js = js;

    public IReadOnlyList<AppNotification> Active => _active;

    public event EventHandler? Changed;

    /// <summary>
    /// Adds and presents an alert toast to the user.
    /// </summary>
    public async Task ShowAsync(
        AppNotificationKind kind,
        string title,
        string message,
        TimeSpan? duration = null)
    {
        // 1. Create and register notification entity in active list
        var notification = new AppNotification(Guid.NewGuid(), kind, title, message);
        _active.Add(notification);
        Changed?.Invoke(this, EventArgs.Empty);

        // 2. Play subtle contextual audio cue via JS interop if available
        try
        {
            await _js.InvokeVoidAsync("pcNotifications.play", kind.ToString().ToLowerInvariant());
        }
        catch (JSDisconnectedException)
        {
            // Circuit shutdown or navigation in progress
        }
        catch (JSException)
        {
            // Optional sound script unavailable
        }
        catch (InvalidOperationException)
        {
            // Non-interactive or pre-render mode
        }

        // 3. Determine auto-dismissal policy:
        // - If duration is explicitly specified, respect it (unless Timeout.InfiniteTimeSpan).
        // - If duration is null, Errors & Warnings STICK indefinitely so the user can read and understand them.
        // - Success and Info stay for a generous 15 seconds so they don't vanish prematurely.
        if (duration.HasValue)
        {
            if (duration.Value != Timeout.InfiniteTimeSpan && duration.Value > TimeSpan.Zero)
            {
                _ = RemoveAfterDelayAsync(notification.Id, duration.Value);
            }
        }
        else
        {
            // Stick errors and warnings permanently; allow 15 seconds for success/info
            if (kind == AppNotificationKind.Success || kind == AppNotificationKind.Info)
            {
                _ = RemoveAfterDelayAsync(notification.Id, TimeSpan.FromSeconds(15));
            }
            // Error and Warning remain until user explicitly clicks the dismiss button (×)
        }
    }

    /// <summary>
    /// Explicitly removes an active notification item from the toast stack.
    /// </summary>
    public void Dismiss(Guid id)
    {
        // 1. Remove targeted notification and notify UI consumers
        if (_active.RemoveAll(notification => notification.Id == id) > 0)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Asynchronously schedules notification removal after a designated duration.
    /// </summary>
    private async Task RemoveAfterDelayAsync(Guid id, TimeSpan duration)
    {
        // 1. Await designated display duration
        await Task.Delay(duration);

        // 2. Dismiss target notification
        Dismiss(id);
    }
}
