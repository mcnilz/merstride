namespace Merstride.Domain;

public static class ResourceIds
{
    public static bool Valid(string? value, char kind) => value is { Length: >= 8 and <= 34 } &&
        value[0] == kind && value[1] == '_' && value.AsSpan(2).IndexOfAnyExcept("0123456789abcdef") < 0;
}
