using System.Net;
using System.Net.Http;
using DofusManager.Core.Services;
using Xunit;

namespace DofusManager.Tests.Services;

public class UpdateServiceTests
{
    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        var handler = new MockHttpMessageHandler(statusCode, content);
        return new HttpClient(handler);
    }

    [Fact]
    public async Task CheckForUpdateAsync_NewerVersionAvailable_ReturnsAvailable()
    {
        var json = CreateReleaseJson("v2.0.0", "DofusQoL-v2.0.0-win-x64.zip",
            "https://github.com/download/zip", "Bug fixes");
        var client = CreateMockHttpClient(HttpStatusCode.OK, json);
        var service = new UpdateService(client, new Version(1, 0, 0));

        var result = await service.CheckForUpdateAsync();

        Assert.True(result.IsUpdateAvailable);
        Assert.NotNull(result.Update);
        Assert.Equal(new Version(2, 0, 0), result.Update.Version);
        Assert.Equal("https://github.com/download/zip", result.Update.DownloadUrl);
        Assert.Equal("Bug fixes", result.Update.ReleaseNotes);
    }

    [Fact]
    public async Task CheckForUpdateAsync_SameVersion_ReturnsUpToDate()
    {
        var json = CreateReleaseJson("v1.0.0", "DofusQoL-v1.0.0-win-x64.zip",
            "https://github.com/download/zip", null);
        var client = CreateMockHttpClient(HttpStatusCode.OK, json);
        var service = new UpdateService(client, new Version(1, 0, 0));

        var result = await service.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.Null(result.Update);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdateAsync_OlderVersion_ReturnsUpToDate()
    {
        var json = CreateReleaseJson("v0.9.0", "DofusQoL-v0.9.0-win-x64.zip",
            "https://github.com/download/zip", null);
        var client = CreateMockHttpClient(HttpStatusCode.OK, json);
        var service = new UpdateService(client, new Version(1, 0, 0));

        var result = await service.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckForUpdateAsync_RateLimited_ReturnsError()
    {
        var client = CreateMockHttpClient(HttpStatusCode.Forbidden, "rate limit");
        var service = new UpdateService(client, new Version(1, 0, 0));

        var result = await service.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Limite", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdateAsync_TooManyRequests_ReturnsError()
    {
        var client = CreateMockHttpClient(HttpStatusCode.TooManyRequests, "");
        var service = new UpdateService(client, new Version(1, 0, 0));

        var result = await service.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdateAsync_NetworkError_ReturnsError()
    {
        var handler = new MockHttpMessageHandler(new HttpRequestException("No internet"));
        var client = new HttpClient(handler);
        var service = new UpdateService(client, new Version(1, 0, 0));

        var result = await service.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("serveur", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdateAsync_InvalidTagFormat_ReturnsError()
    {
        var json = CreateReleaseJson("not-a-version", "DofusQoL.zip",
            "https://github.com/download/zip", null);
        var client = CreateMockHttpClient(HttpStatusCode.OK, json);
        var service = new UpdateService(client, new Version(1, 0, 0));

        var result = await service.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("invalide", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdateAsync_NoZipAsset_ReturnsError()
    {
        var json = """
        {
            "tag_name": "v2.0.0",
            "assets": [],
            "body": "No assets",
            "published_at": "2025-01-01T00:00:00Z"
        }
        """;
        var client = CreateMockHttpClient(HttpStatusCode.OK, json);
        var service = new UpdateService(client, new Version(1, 0, 0));

        var result = await service.CheckForUpdateAsync();

        Assert.False(result.IsUpdateAvailable);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("fichier", result.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdateAsync_Cancelled_ReturnsError()
    {
        var json = CreateReleaseJson("v2.0.0", "DofusQoL-v2.0.0-win-x64.zip",
            "https://github.com/download/zip", null);
        var client = CreateMockHttpClient(HttpStatusCode.OK, json);
        var service = new UpdateService(client, new Version(1, 0, 0));
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var result = await service.CheckForUpdateAsync(cts.Token);

        Assert.False(result.IsUpdateAvailable);
        Assert.NotNull(result.ErrorMessage);
    }

    private static string CreateReleaseJson(string tagName, string assetName, string downloadUrl, string? body)
    {
        var bodyJson = body is null ? "null" : $"\"{body}\"";
        return $$"""
        {
            "tag_name": "{{tagName}}",
            "assets": [
                {
                    "name": "{{assetName}}",
                    "browser_download_url": "{{downloadUrl}}",
                    "size": 1024000
                }
            ],
            "body": {{bodyJson}},
            "published_at": "2025-01-01T00:00:00Z"
        }
        """;
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode? _statusCode;
        private readonly string? _content;
        private readonly Exception? _exception;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        public MockHttpMessageHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_exception is not null)
                throw _exception;

            return Task.FromResult(new HttpResponseMessage(_statusCode!.Value)
            {
                Content = new StringContent(_content!)
            });
        }
    }
}
