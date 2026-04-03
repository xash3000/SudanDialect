using System.Text;
using System.Text.RegularExpressions;

namespace SudanDialect.Api.Utilities;

public static class ArabicTextNormalizer
{
    public const string AlefVariantsPattern = "[أإآٱ]";
    public const string YehVariantsPattern = "[ىئ]";
    public const string TehMarbutaPattern = "ة";
    public const string PunctuationPattern = @"[\p{P}\p{S}]";
    public const string ArabicDiacriticsPattern = @"[\u064B-\u065F\u0670\u06D6-\u06ED]";
    public const string TatweelPattern = "\u0640";
    public const string HamzaOnWawPattern = "ؤ";
    public const string StandaloneHamzaPattern = "ء";
    public const string RepeatedCharactersPattern = @"([\u0621-\u064A0-9])\1+";
    public const string BidiControlCharsPattern = @"[\u061C\u200E\u200F\u202A-\u202E\u2066-\u2069]";
    public const string MultiWhitespacePattern = @"\s+";

    private static readonly Regex AlefVariantsRegex = new(AlefVariantsPattern, RegexOptions.Compiled);
    private static readonly Regex YehVariantsRegex = new(YehVariantsPattern, RegexOptions.Compiled);
    private static readonly Regex TehMarbutaRegex = new(TehMarbutaPattern, RegexOptions.Compiled);
    private static readonly Regex PunctuationRegex = new(PunctuationPattern, RegexOptions.Compiled);
    private static readonly Regex ArabicDiacriticsRegex = new(ArabicDiacriticsPattern, RegexOptions.Compiled);
    private static readonly Regex TatweelRegex = new(TatweelPattern, RegexOptions.Compiled);
    private static readonly Regex HamzaOnWawRegex = new(HamzaOnWawPattern, RegexOptions.Compiled);
    private static readonly Regex StandaloneHamzaRegex = new(StandaloneHamzaPattern, RegexOptions.Compiled);
    private static readonly Regex RepeatedCharactersRegex = new(RepeatedCharactersPattern, RegexOptions.Compiled);
    private static readonly Regex BidiControlCharsRegex = new(BidiControlCharsPattern, RegexOptions.Compiled);
    private static readonly Regex MultiWhitespaceRegex = new(MultiWhitespacePattern, RegexOptions.Compiled);

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var normalized = input;

        // 1) Normalize Alef variants: أ، إ، آ، ٱ -> ا
        normalized = AlefVariantsRegex.Replace(normalized, "ا");

        // 2) Normalize Yeh variants: ى، ئ -> ي
        normalized = YehVariantsRegex.Replace(normalized, "ي");

        // 3) Normalize Teh Marbuta: ة -> ه
        normalized = TehMarbutaRegex.Replace(normalized, "ه");

        // 4) Remove punctuation and trim whitespace.
        normalized = PunctuationRegex.Replace(normalized, " ");
        normalized = MultiWhitespaceRegex.Replace(normalized, " ").Trim();

        // 5) Remove Arabic diacritics (Tashkeel).
        normalized = ArabicDiacriticsRegex.Replace(normalized, string.Empty);

        // 6) Remove Tatweel/Kashida.
        normalized = TatweelRegex.Replace(normalized, string.Empty);

        // 7) Convert Eastern Arabic numerals (٠-٩) to Western numerals (0-9).
        normalized = ConvertEasternArabicNumeralsToWestern(normalized);

        // 8) Normalize Hamza on Waw: ؤ -> و
        normalized = HamzaOnWawRegex.Replace(normalized, "و");

        // 9) Normalize standalone Hamza: ء -> ا
        normalized = StandaloneHamzaRegex.Replace(normalized, "ا");

        // 10) Reduce repeated characters (e.g., ببب -> ب).
        normalized = RepeatedCharactersRegex.Replace(normalized, "$1");

        // 11) Remove invisible BiDi control characters.
        normalized = BidiControlCharsRegex.Replace(normalized, string.Empty);

        return MultiWhitespaceRegex.Replace(normalized, " ").Trim();
    }

    private static string ConvertEasternArabicNumeralsToWestern(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var builder = new StringBuilder(input.Length);

        foreach (var character in input)
        {
            builder.Append(character switch
            {
                '٠' => '0',
                '١' => '1',
                '٢' => '2',
                '٣' => '3',
                '٤' => '4',
                '٥' => '5',
                '٦' => '6',
                '٧' => '7',
                '٨' => '8',
                '٩' => '9',
                _ => character
            });
        }

        return builder.ToString();
    }
}
