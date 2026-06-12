namespace UrlShortener.Api.Services;

/// <summary>
/// Converts between numeric IDs and base62 string representation.
/// Alphabet: [0-9, a-z, A-Z] (62 characters).
/// Always produces/expects exactly 7-character strings.
/// </summary>
public static class Base62Converter
{
    private const int Length = 7;
    private const string Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly Dictionary<char, long> CharToValue = Alphabet
        .Select((c, i) => (c, i))
        .ToDictionary(x => x.c, x => (long)x.i);

    /// <summary>
    /// Encodes a numeric ID to a fixed-length base62 string (padded with leading '0's).
    /// </summary>
    public static string Encode(long id)
    {
        if (id < 0)
            throw new ArgumentOutOfRangeException(nameof(id), "ID must be non-negative.");

        var chars = new char[Length];
        for (var i = Length - 1; i >= 0; i--)
        {
            chars[i] = Alphabet[(int)(id % Alphabet.Length)];
            id /= Alphabet.Length;
        }
        return new string(chars);
    }

    /// <summary>
    /// Tries to decode a base62 string back to a numeric ID.
    /// Returns false if the input is null, has incorrect length, or contains invalid characters.
    /// </summary>
    public static bool TryDecode(string? input, out long id)
    {
        id = 0;

        if (string.IsNullOrEmpty(input) || input.Length != Length)
            return false;

        foreach (var c in input)
        {
            if (!CharToValue.TryGetValue(c, out var value))
                return false;
            id = id * Alphabet.Length + value;
        }

        return true;
    }
}
