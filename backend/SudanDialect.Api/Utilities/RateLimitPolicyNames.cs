namespace SudanDialect.Api.Utilities;

public static class RateLimitPolicyNames
{
    public const string WordsBrowsePerIp = "words-browse-per-ip";
    public const string WordsSearchPerIp = "words-search-per-ip";
    public const string WordsSemanticSearchPerIp = "words-semantic-search-per-ip";
    public const string WordsGetByIdPerIp = "words-get-by-id-per-ip";
}
