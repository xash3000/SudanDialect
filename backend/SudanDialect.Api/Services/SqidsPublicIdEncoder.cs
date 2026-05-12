using Microsoft.Extensions.Options;
using Sqids;
using SudanDialect.Api.Configuration;
using SudanDialect.Api.Interfaces.Services;
using System.Security.Cryptography;
using System.Text;

namespace SudanDialect.Api.Services;

public sealed class SqidsPublicIdEncoder : IPublicIdEncoder
{
    private const string BaseAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    private readonly SqidsEncoder<int> _encoder;

    public SqidsPublicIdEncoder(IOptions<PublicIdOptions> options, IConfiguration configuration)
    {
        var configured = options.Value;
        var minLength = configured.MinLength < 0 ? 0 : configured.MinLength;
        var signingKey = configuration["Jwt:SigningKey"];

        if (string.IsNullOrWhiteSpace(signingKey))
        {
            throw new InvalidOperationException("JWT signing key is required for public ID generation.");
        }

        var alphabet = BuildAlphabetFromSigningKey(signingKey);

        _encoder = new SqidsEncoder<int>(new SqidsOptions
        {
            MinLength = minLength,
            Alphabet = alphabet
        });
    }

    public string EncodeWordId(int id)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Word id must be a positive integer.");
        }

        return _encoder.Encode(id);
    }

    public bool TryDecodeWordId(string encodedId, out int id)
    {
        id = 0;

        if (string.IsNullOrWhiteSpace(encodedId))
        {
            return false;
        }

        var decoded = _encoder.Decode(encodedId.Trim());
        if (decoded.Count != 1)
        {
            return false;
        }

        var candidate = decoded[0];
        if (candidate <= 0)
        {
            return false;
        }

        id = candidate;
        return true;
    }

    internal static string BuildAlphabetFromSigningKey(string signingKey)
    {
        var characters = BaseAlphabet.ToCharArray();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(signingKey));
        var seedValue = BitConverter.ToInt32(hash, 0);
        var random = new Random(seedValue);

        for (var index = characters.Length - 1; index > 0; index -= 1)
        {
            var swapIndex = random.Next(index + 1);
            (characters[index], characters[swapIndex]) = (characters[swapIndex], characters[index]);
        }

        return new string(characters);
    }
}
