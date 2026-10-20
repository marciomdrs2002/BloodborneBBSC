namespace BloodborneLauncher.Core;

public sealed record Effect(string Key, string Title, bool Default, string? Hint = null);
