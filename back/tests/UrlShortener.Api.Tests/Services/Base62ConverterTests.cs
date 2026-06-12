using System.Text.RegularExpressions;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Tests.Services;

public class Base62ConverterTests
{
    [Theory]
    [InlineData(0, "0000000")]
    [InlineData(1, "0000001")]
    [InlineData(62, "0000010")]
    [InlineData(63, "0000011")]
    public void Encode_ShouldProduce7CharacterString(long id, string expected)
    {
        var result = Base62Converter.Encode(id);
        Assert.Equal(expected, result);
        Assert.Equal(7, result.Length);
    }

    [Fact]
    public void Encode_BookExample_2009215674938_Returns_zn9edcu()
    {
        var result = Base62Converter.Encode(2009215674938);
        Assert.Equal("zn9edcu", result);
    }

    [Fact]
    public void Encode_NegativeId_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Base62Converter.Encode(-1));
    }

    [Fact]
    public void TryDecode_ValidString_ReturnsTrueAndCorrectId()
    {
        var success = Base62Converter.TryDecode("zn9edcu", out var id);
        Assert.True(success);
        Assert.Equal(2009215674938, id);
    }

    [Fact]
    public void TryDecode_AllZeros_ReturnsZero()
    {
        var success = Base62Converter.TryDecode("0000000", out var id);
        Assert.True(success);
        Assert.Equal(0, id);
    }

    [Fact]
    public void TryDecode_EmptyString_ReturnsFalse()
    {
        var success = Base62Converter.TryDecode("", out _);
        Assert.False(success);
    }

    [Fact]
    public void TryDecode_NullString_ReturnsFalse()
    {
        var success = Base62Converter.TryDecode(null, out _);
        Assert.False(success);
    }

    [Fact]
    public void TryDecode_ShortString_ReturnsFalse()
    {
        var success = Base62Converter.TryDecode("abc", out _);
        Assert.False(success);
    }

    [Fact]
    public void TryDecode_InvalidCharacters_ReturnsFalse()
    {
        var success = Base62Converter.TryDecode("!!!!!!!", out _);
        Assert.False(success);
    }

    [Fact]
    public void TryDecode_InvalidCharactersWithin_ReturnsFalse()
    {
        var success = Base62Converter.TryDecode("abc+def", out _);
        Assert.False(success);
    }

    [Fact]
    public void TryDecode_Spaces_ReturnsFalse()
    {
        var success = Base62Converter.TryDecode("abc def", out _);
        Assert.False(success);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(999)]
    [InlineData(1000000)]
    [InlineData(2009215674938)]
    [InlineData(3521614606207)]
    public void EncodeAndDecode_Roundtrip_ReturnsOriginalId(long id)
    {
        var encoded = Base62Converter.Encode(id);
        Assert.Equal(7, encoded.Length);

        var success = Base62Converter.TryDecode(encoded, out var decoded);
        Assert.True(success);
        Assert.Equal(id, decoded);
    }

    [Fact]
    public void Encode_ProducesOnlyValidCharacters()
    {
        var encoded = Base62Converter.Encode(123456789);
        Assert.Equal("008m0Kx", encoded);
        Assert.Matches("^[0-9a-zA-Z]{7}$", encoded);
    }

    [Fact]
    public void Encode_SequentialIds_ProducesDistinctResults()
    {
        var ids = Enumerable.Range(0, 1000).Select(i => (long)i).ToList();
        var results = ids.Select(Base62Converter.Encode).ToList();
        Assert.Equal(1000, results.Distinct().Count());
    }
}
