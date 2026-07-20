namespace ArasPatchUpgradeAssistant.Helpers;

public static class PasswordMask
{
    public static string Create(string? value) =>
        new('*', value?.Length ?? 0);
}
