using FluentAssertions;
using SudanDialect.Api.Utilities;
using Xunit;

namespace SudanDialect.Tests.Unit.Utilities;

public class ArabicTextNormalizerTests
{
    [Fact]
    public void Normalize_ShouldReturnEmptyString_WhenInputIsNull()
    {
        var result = ArabicTextNormalizer.Normalize(null);
        result.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_ShouldReturnEmptyString_WhenInputIsEmpty()
    {
        var result = ArabicTextNormalizer.Normalize("");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_ShouldReturnEmptyString_WhenInputIsWhitespace()
    {
        var result = ArabicTextNormalizer.Normalize("   ");
        result.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_ShouldNormalizeAlefVariants_ToAlef()
    {
        var result = ArabicTextNormalizer.Normalize("أهل");
        result.Should().Be("اهل");

        result = ArabicTextNormalizer.Normalize("إسلام");
        result.Should().Be("اسلام");

        result = ArabicTextNormalizer.Normalize("آمال");
        result.Should().Be("امال");

        result = ArabicTextNormalizer.Normalize("ٱنطلق");
        result.Should().Be("انطلق");
    }

    [Fact]
    public void Normalize_ShouldNormalizeYehVariants_ToYeh()
    {
        var result = ArabicTextNormalizer.Normalize("على");
        result.Should().Be("علي");

        result = ArabicTextNormalizer.Normalize("بيت");
        result.Should().Be("بيت");
    }

    [Fact]
    public void Normalize_ShouldNormalizeTehMarbuta_ToHeh()
    {
        var result = ArabicTextNormalizer.Normalize("ساعة");
        result.Should().Be("ساعه");

        result = ArabicTextNormalizer.Normalize("مسطرة");
        result.Should().Be("مسطره");
    }

    [Fact]
    public void Normalize_ShouldRemovePunctuation()
    {
        var result = ArabicTextNormalizer.Normalize("مرحبا! كيف حالك؟");
        result.Should().Be("مرحبا كيف حالك");

        result = ArabicTextNormalizer.Normalize("-test.");
        result.Should().Be("test");
    }

    [Fact]
    public void Normalize_ShouldRemoveArabicDiacritics()
    {
        var result = ArabicTextNormalizer.Normalize("مَكْتُوبٌ");
        result.Should().Be("مكتوب");

        result = ArabicTextNormalizer.Normalize("مُحَمَّدٌ");
        result.Should().Be("محمد");
    }

    [Fact]
    public void Normalize_ShouldRemoveTatweel()
    {
        var result = ArabicTextNormalizer.Normalize("مـكتب");
        result.Should().Be("مكتب");
    }

    [Fact]
    public void Normalize_ShouldConvertEasternArabicNumerals_ToWestern()
    {
        var result = ArabicTextNormalizer.Normalize("٠١٢٣٤٥٦٧٨٩");
        result.Should().Be("0123456789");

        result = ArabicTextNormalizer.Normalize("سنة ١٩٩٠");
        result.Should().Be("سنه 1990");
    }

    [Fact]
    public void Normalize_ShouldNormalizeHamzaOnWaw_ToWaw()
    {
        var result = ArabicTextNormalizer.Normalize("ؤمن");
        result.Should().Be("ومن");
    }

    [Fact]
    public void Normalize_ShouldNormalizeStandaloneHamza_ToAlef()
    {
        var result = ArabicTextNormalizer.Normalize("ءامن");
        result.Should().Be("اامن");
    }

    [Fact]
    public void Normalize_ShouldPreserveRepeatedCharacters()
    {
        var result = ArabicTextNormalizer.Normalize("ممتاز");
        result.Should().Be("ممتاز");

        result = ArabicTextNormalizer.Normalize("ببب");
        result.Should().Be("ببب");
    }

    [Fact]
    public void Normalize_ShouldRemoveBidiControlCharacters()
    {
        var result = ArabicTextNormalizer.Normalize("مرحبا\u200E");
        result.Should().Be("مرحبا");

        result = ArabicTextNormalizer.Normalize("test\u202A");
        result.Should().Be("test");
    }

    [Fact]
    public void Normalize_ShouldNormalizeMultipleWhitespace()
    {
        var result = ArabicTextNormalizer.Normalize("مرحبا    كيف    حالك");
        result.Should().Be("مرحبا كيف حالك");
    }

    [Fact]
    public void Normalize_ShouldHandleComplexArabicText()
    {
        var result = ArabicTextNormalizer.Normalize("السلامُ عليكُمُ");
        result.Should().Be("السلام عليكم");
    }

    [Theory]
    [InlineData("٠١٢٣٤٥٦٧٨٩", "0123456789")]
    [InlineData("١٢٣", "123")]
    [InlineData("٠", "0")]
    [InlineData("٩٨٧", "987")]
    public void Normalize_ShouldConvertAllEasternArabicNumerals(string input, string expected)
    {
        var result = ArabicTextNormalizer.Normalize(input);
        result.Should().Be(expected);
    }

    [Fact]
    public void Normalize_ShouldPreserveLatinCharacters()
    {
        var result = ArabicTextNormalizer.Normalize("مرحبا Hello");
        result.Should().Be("مرحبا Hello");
    }

    [Fact]
    public void Normalize_ShouldHandleMixedContent()
    {
        var result = ArabicTextNormalizer.Normalize("Test ١٢٣ مرحبا!");
        result.Should().Be("Test 123 مرحبا");
    }
}
