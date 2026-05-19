using SudanDialect.Api.Dtos;
using SudanDialect.Api.Interfaces.Repositories;
using SudanDialect.Api.Interfaces.Services;
using SudanDialect.Api.Models;
using SudanDialect.Api.Utilities;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SudanDialect.Api.Services;

public sealed class WordService : IWordService
{
    private const int MaxQueryLength = 200;
    private const int MaxResults = 10;
    private const int MaxBrowsePageSize = 60;
    private const int MaxFeedbackLength = 2000;
    private const int MaxSuggestionHeadwordLength = 200;
    private const int MaxSuggestionDefinitionLength = 4000;
    private const int MaxSuggestionEmailLength = 320;

    private static readonly Regex ArabicLetterRegex = new("^[\\u0621-\\u064A]$", RegexOptions.Compiled);
    private static readonly CompareInfo ArabicCompareInfo = CultureInfo.GetCultureInfo("ar").CompareInfo;

    private readonly IWordRepository _wordRepository;
    private readonly IPublicIdEncoder _publicIdEncoder;
    private readonly ITurnstileVerificationService _turnstileVerificationService;

    public WordService(
        IWordRepository wordRepository,
        IPublicIdEncoder publicIdEncoder,
        ITurnstileVerificationService turnstileVerificationService)
    {
        _wordRepository = wordRepository;
        _publicIdEncoder = publicIdEncoder;
        _turnstileVerificationService = turnstileVerificationService;
    }

    public async Task<WordDetailsDto?> GetByPublicIdAsync(string publicId, CancellationToken cancellationToken = default)
    {
        var id = DecodeWordPublicIdOrThrow(publicId);

        var word = await _wordRepository.GetActiveByIdAsync(id, cancellationToken);
        if (word is null)
        {
            return null;
        }

        return new WordDetailsDto
        {
            Id = _publicIdEncoder.EncodeWordId(word.Id),
            Headword = word.Headword,
            Definition = word.Definition
        };
    }

    public async Task<IReadOnlyList<WordSearchResultDto>> SearchAsync(string? rawQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return Array.Empty<WordSearchResultDto>();
        }

        if (rawQuery.Length > MaxQueryLength)
        {
            throw new ArgumentException($"Query length cannot exceed {MaxQueryLength} characters.", nameof(rawQuery));
        }

        var normalizedQuery = ArabicTextNormalizer.Normalize(rawQuery);

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<WordSearchResultDto>();
        }

        var searchResults = await _wordRepository.SearchActiveByNormalizedQueryAsync(
            normalizedQuery,
            MaxResults,
            cancellationToken);

        return searchResults
            .Select(result => new WordSearchResultDto
            {
                Id = _publicIdEncoder.EncodeWordId(result.Id),
                Headword = result.Headword,
                SimilarityScore = result.SimilarityScore
            })
            .ToList();
    }

    public async Task<WordBrowsePageDto> BrowseByLetterAsync(
        string? rawLetter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawLetter))
        {
            throw new ArgumentException("Letter is required.", nameof(rawLetter));
        }

        if (page <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(page), "Page must be a positive integer.");
        }

        if (pageSize <= 0 || pageSize > MaxBrowsePageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), $"Page size must be between 1 and {MaxBrowsePageSize}.");
        }

        var trimmedLetter = rawLetter.Trim();
        if (trimmedLetter.Length != 1 || !ArabicLetterRegex.IsMatch(trimmedLetter))
        {
            throw new ArgumentException("Letter must be a single Arabic character.", nameof(rawLetter));
        }

        var normalizedLetter = ArabicTextNormalizer.Normalize(trimmedLetter);
        if (string.IsNullOrWhiteSpace(normalizedLetter))
        {
            return new WordBrowsePageDto
            {
                Items = [],
                Page = page,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 0
            };
        }

        var pagedResults = await _wordRepository.GetActiveByFirstLetterPagedAsync(
            trimmedLetter,
            normalizedLetter,
            page,
            pageSize,
            cancellationToken);

        if (pagedResults.TotalPages == 0)
        {
            return new WordBrowsePageDto
            {
                Items = [],
                Page = 1,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 0
            };
        }

        var pagedItems = pagedResults.Items
            .Select(word => new WordSummaryDto
            {
                Id = _publicIdEncoder.EncodeWordId(word.Id),
                Headword = word.Headword
            })
            .ToList();

        return new WordBrowsePageDto
        {
            Items = pagedItems,
            Page = pagedResults.BoundedPage,
            PageSize = pageSize,
            TotalCount = pagedResults.TotalCount,
            TotalPages = pagedResults.TotalPages
        };
    }

    public async Task<bool> SubmitFeedbackAsync(
        string publicWordId,
        string? feedbackText,
        string? captchaToken,
        string? remoteIp,
        CancellationToken cancellationToken = default)
    {
        var wordId = DecodeWordPublicIdOrThrow(publicWordId);

        if (string.IsNullOrWhiteSpace(feedbackText))
        {
            throw new ArgumentException("Feedback text is required.", nameof(feedbackText));
        }

        var normalizedFeedback = feedbackText.Trim();
        if (normalizedFeedback.Length > MaxFeedbackLength)
        {
            throw new ArgumentException($"Feedback text cannot exceed {MaxFeedbackLength} characters.", nameof(feedbackText));
        }

        if (string.IsNullOrWhiteSpace(captchaToken))
        {
            throw new ArgumentException("Captcha token is required.", nameof(captchaToken));
        }

        var captchaValid = await _turnstileVerificationService.VerifyAsync(captchaToken, remoteIp, cancellationToken);
        if (!captchaValid)
        {
            throw new ArgumentException("Captcha verification failed.", nameof(captchaToken));
        }

        var word = await _wordRepository.GetActiveByIdAsync(wordId, cancellationToken);
        if (word is null)
        {
            return false;
        }

        await _wordRepository.AddFeedbackAsync(
            new Feedback
            {
                WordId = wordId,
                FeedbackText = normalizedFeedback
            },
            cancellationToken);

        return true;
    }

    public async Task<bool> SubmitSuggestionAsync(
        string? headword,
        string? definition,
        string? email,
        string? captchaToken,
        string? remoteIp,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(headword))
        {
            throw new ArgumentException("Headword is required.", nameof(headword));
        }

        if (string.IsNullOrWhiteSpace(definition))
        {
            throw new ArgumentException("Definition is required.", nameof(definition));
        }

        var normalizedHeadword = headword.Trim();
        if (normalizedHeadword.Length > MaxSuggestionHeadwordLength)
        {
            throw new ArgumentException($"Headword cannot exceed {MaxSuggestionHeadwordLength} characters.", nameof(headword));
        }

        var normalizedDefinition = definition.Trim();
        if (normalizedDefinition.Length > MaxSuggestionDefinitionLength)
        {
            throw new ArgumentException($"Definition cannot exceed {MaxSuggestionDefinitionLength} characters.", nameof(definition));
        }

        string? normalizedEmail = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            normalizedEmail = email.Trim();

            if (normalizedEmail.Length > MaxSuggestionEmailLength)
            {
                throw new ArgumentException($"Email cannot exceed {MaxSuggestionEmailLength} characters.", nameof(email));
            }

            var emailAttribute = new EmailAddressAttribute();
            if (!emailAttribute.IsValid(normalizedEmail))
            {
                throw new ArgumentException("Email is not valid.", nameof(email));
            }
        }

        if (string.IsNullOrWhiteSpace(captchaToken))
        {
            throw new ArgumentException("Captcha token is required.", nameof(captchaToken));
        }

        var captchaValid = await _turnstileVerificationService.VerifyAsync(captchaToken, remoteIp, cancellationToken);
        if (!captchaValid)
        {
            throw new ArgumentException("Captcha verification failed.", nameof(captchaToken));
        }

        await _wordRepository.AddSuggestionAsync(
            new WordSuggestion
            {
                Headword = normalizedHeadword,
                Definition = normalizedDefinition,
                Email = normalizedEmail,
                Resolved = false
            },
            cancellationToken);

        return true;
    }

    private int DecodeWordPublicIdOrThrow(string publicId)
    {
        if (!_publicIdEncoder.TryDecodeWordId(publicId, out var decodedId))
        {
            throw new ArgumentException("Word id is invalid.", nameof(publicId));
        }

        return decodedId;
    }
}
