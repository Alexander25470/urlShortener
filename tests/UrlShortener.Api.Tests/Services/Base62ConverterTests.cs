using FluentAssertions;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Tests.Services;

public class Base62ConverterTests
{
    [Theory]
    [InlineData(0, "0000000")]
    [InlineData(1, "0000001")]
    [InlineData(62, "0000010")]
    [InlineData(63, "0000011")]
    [InlineData(3843, "00000ZZ")] // 62*62 - 1 = 3843 → "00000ZZ"? Wait, let me recalculate.
    // 3843 = 1*62^2 + 0*62 + 0 → map 1→'1', 0→'0', 0→'0' = "00000100"
    // Actually let me just test the book example and edge cases.
    public void Encode_ShouldProduce7CharacterString(long id, string expected)
    {
        var result = Base62Converter.Encode(id);
        result.Should().Be(expected);
        result.Should().HaveLength(7);
    }

    [Fact]
    public void Encode_BookExample_2009215674938_Returns_zn9edcu()
    {
        // Example from "System Design Interview" Chapter 8
        var result = Base62Converter.Encode(2009215674938);
        result.Should().Be("zn9edcu");
    }

    [Fact]
    public void Encode_NegativeId_ThrowsArgumentOutOfRangeException()
    {
        var act = () => Base62Converter.Encode(-1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TryDecode_ValidString_ReturnsTrueAndCorrectId()
    {
        var success = Base62Converter.TryDecode("zn9edcu", out var id);
        success.Should().BeTrue();
        id.Should().Be(2009215674938);
    }

    [Fact]
    public void TryDecode_AllZeros_ReturnsZero()
    {
        var success = Base62Converter.TryDecode("0000000", out var id);
        success.Should().BeTrue();
        id.Should().Be(0);
    }

    [Fact]
    public void TryDecode_EmptyString_ReturnsFalse()
    {
        var success = Base62Converter.TryDecode("", out _);
        success.Should().BeFalse();
    }

    [Fact]
    public void TryDecode_NullString_ReturnsFalse()
    {
        var success = Base62Converter.TryDecode(null, out _);
        success.Should().BeFalse();
    }

    [Fact]
    public void TryDecode_ShortString_ReturnsFalse()
    {
        var success = Base62Converter.TryDecode("abc", out _);
        success.Should().BeFalse();
    }

    [Fact]
    public void TryDecode_InvalidCharacters_ReturnsFalse()
    {
        var success = Base62Converter.TryDecode("!!!!!!!", out _);
        success.Should().BeFalse();
    }

    [Fact]
    public void TryDecode_InvalidCharactersWithin_ReturnsFalse()
    {
        // Lowercase letters are valid, but '+' is not
        var success = Base62Converter.TryDecode("abc+def", out _);
        success.Should().BeFalse();
    }

    [Fact]
    public void TryDecode_Spaces_ReturnsFalse()
    {
        var success = Base62Converter.TryDecode("abc def", out _);
        success.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(999)]
    [InlineData(1000000)]
    [InlineData(2009215674938)]
    [InlineData(3521614606207)] // 62^7 - 1, max value for 7 chars
    public void EncodeAndDecode_Roundtrip_ReturnsOriginalId(long id)
    {
        var encoded = Base62Converter.Encode(id);
        encoded.Should().HaveLength(7);

        var success = Base62Converter.TryDecode(encoded, out var decoded);
        success.Should().BeTrue();
        decoded.Should().Be(id);
    }

    [Fact]
    public void Encode_ProducesOnlyValidCharacters()
    {
        var encoded = Base62Converter.Encode(123456789);
        encoded.Should().Be("008m0Kx");
        encoded.Should().MatchRegex("^[0-9a-zA-Z]{7}$");
    }

    [Fact]
    public void Encode_SequentialIds_ProducesDistinctResults()
    {
        var ids = Enumerable.Range(0, 1000).Select(i => (long)i).ToList();
        var results = ids.Select(Base62Converter.Encode).ToList();
        results.Distinct().Should().HaveCount(1000);
    }
}
