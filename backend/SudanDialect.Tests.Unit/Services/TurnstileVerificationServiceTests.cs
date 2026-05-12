using FluentAssertions;
using Microsoft.Extensions.Options;
using SudanDialect.Api.Configuration;
using SudanDialect.Api.Services;
using System.Net;
using System.Text;
using Xunit;

namespace SudanDialect.Tests.Unit.Services;

public class TurnstileVerificationServiceTests
{
    private sealed class RecordingHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public int CallCount { get; private set; }
        public HttpMethod? LastMethod { get; private set; }
        public Uri? LastRequestUri { get; private set; }
        public string? LastContentTypeMediaType { get; private set; }
        public string? LastBody { get; private set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public RecordingHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount += 1;
            LastMethod = request.Method;
            LastRequestUri = request.RequestUri;
            LastContentTypeMediaType = request.Content?.Headers.ContentType?.MediaType;
            LastCancellationToken = cancellationToken;

            if (request.Content is not null)
            {
                LastBody = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            return await _responder(request, cancellationToken);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyAsync_ShouldReturnFalseAndNotCallHttp_WhenTokenIsNullOrWhitespace(string? token)
    {
        var handler = new RecordingHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called"));
        using var httpClient = new HttpClient(handler);
        var sut = new TurnstileVerificationService(httpClient, Options.Create(new TurnstileOptions { SecretKey = "secret" }));

        var result = await sut.VerifyAsync(token!, remoteIp: "1.2.3.4", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
        handler.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyAsync_ShouldReturnFalseAndNotCallHttp_WhenSecretKeyIsNullOrWhitespace(string? secretKey)
    {
        var handler = new RecordingHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called"));
        using var httpClient = new HttpClient(handler);
        var sut = new TurnstileVerificationService(httpClient, Options.Create(new TurnstileOptions { SecretKey = secretKey! }));

        var result = await sut.VerifyAsync("token", remoteIp: "1.2.3.4", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
        handler.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyAsync_ShouldNotIncludeRemoteIp_WhenRemoteIpIsNullOrWhitespace(string? remoteIp)
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\": false}", Encoding.UTF8, "application/json")
        }));

        using var httpClient = new HttpClient(handler);
        var sut = new TurnstileVerificationService(httpClient, Options.Create(new TurnstileOptions { SecretKey = "secret" }));

        var result = await sut.VerifyAsync("token", remoteIp: remoteIp, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
        handler.CallCount.Should().Be(1);
        handler.LastBody.Should().NotContain("remoteip=");
    }

    [Fact]
    public async Task VerifyAsync_ShouldIncludeRemoteIp_WhenRemoteIpIsProvided()
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\": false}", Encoding.UTF8, "application/json")
        }));

        using var httpClient = new HttpClient(handler);
        var sut = new TurnstileVerificationService(httpClient, Options.Create(new TurnstileOptions { SecretKey = "secret" }));

        var result = await sut.VerifyAsync("token", remoteIp: "1.2.3.4", cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
        handler.CallCount.Should().Be(1);
        handler.LastBody.Should().Contain("remoteip=1.2.3.4");
    }

    [Fact]
    public async Task VerifyAsync_ShouldSendExpectedRequest_WhenTokenAndSecretAreValid()
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\": false}", Encoding.UTF8, "application/json")
        }));

        using var httpClient = new HttpClient(handler);
        var sut = new TurnstileVerificationService(httpClient, Options.Create(new TurnstileOptions { SecretKey = "secret" }));

        await sut.VerifyAsync("token", remoteIp: null, cancellationToken: TestContext.Current.CancellationToken);

        handler.CallCount.Should().Be(1);
        handler.LastMethod.Should().Be(HttpMethod.Post);
        handler.LastRequestUri.Should().Be(new Uri("https://challenges.cloudflare.com/turnstile/v0/siteverify"));
        handler.LastContentTypeMediaType.Should().Be("application/x-www-form-urlencoded");
        handler.LastBody.Should().Contain("secret=secret");
        handler.LastBody.Should().Contain("response=token");
    }

    [Fact]
    public async Task VerifyAsync_ShouldReturnFalse_WhenResponseStatusIsNotSuccess()
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{not-json", Encoding.UTF8, "application/json")
        }));

        using var httpClient = new HttpClient(handler);
        var sut = new TurnstileVerificationService(httpClient, Options.Create(new TurnstileOptions { SecretKey = "secret" }));

        var result = await sut.VerifyAsync("token", remoteIp: null, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_ShouldReturnTrue_WhenResponseSuccessIsTrue()
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\": true}", Encoding.UTF8, "application/json")
        }));

        using var httpClient = new HttpClient(handler);
        var sut = new TurnstileVerificationService(httpClient, Options.Create(new TurnstileOptions { SecretKey = "secret" }));

        var result = await sut.VerifyAsync("token", remoteIp: null, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyAsync_ShouldReturnFalse_WhenResponseSuccessIsFalse()
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\": false}", Encoding.UTF8, "application/json")
        }));

        using var httpClient = new HttpClient(handler);
        var sut = new TurnstileVerificationService(httpClient, Options.Create(new TurnstileOptions { SecretKey = "secret" }));

        var result = await sut.VerifyAsync("token", remoteIp: null, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_ShouldReturnFalse_WhenResponsePayloadOmitsSuccess()
    {
        var handler = new RecordingHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        }));

        using var httpClient = new HttpClient(handler);
        var sut = new TurnstileVerificationService(httpClient, Options.Create(new TurnstileOptions { SecretKey = "secret" }));

        var result = await sut.VerifyAsync("token", remoteIp: null, cancellationToken: TestContext.Current.CancellationToken);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_ShouldCancelHttpRequest_WhenCancellationTokenIsCanceled()
    {
        var handlerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var handler = new RecordingHttpMessageHandler(async (_, cancellationToken) =>
        {
            handlerStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\": true}", Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler);
        var sut = new TurnstileVerificationService(httpClient, Options.Create(new TurnstileOptions { SecretKey = "secret" }));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var token = cts.Token;

        var verifyTask = sut.VerifyAsync("token", remoteIp: null, cancellationToken: token);
        await handlerStarted.Task;

        cts.Cancel();

        await FluentActions.Awaiting(async () => await verifyTask)
            .Should().ThrowAsync<OperationCanceledException>();
    }
}
