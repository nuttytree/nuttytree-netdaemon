namespace System;

public static class StringExtensions
{
    public static bool CaseInsensitiveEquals(this string? theString, string? value)
        => (theString == null && value == null) || theString?.Equals(value, StringComparison.OrdinalIgnoreCase) == true;
}
