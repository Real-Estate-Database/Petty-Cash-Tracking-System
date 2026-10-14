namespace PettyCashDatabase.Shared.Auth;

public static class Permissions
{
    public const string CounterView = "Counter.View";
    public const string WeatherView = "Weather.View";
    public const string UsersUnlock = "Users.Unlock";

    public static readonly IReadOnlyList<string> All =
    [
        CounterView,
        WeatherView,
        UsersUnlock
    ];
}
