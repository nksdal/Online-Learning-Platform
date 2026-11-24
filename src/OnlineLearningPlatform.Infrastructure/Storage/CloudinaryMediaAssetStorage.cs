using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OnlineLearningPlatform.Application.Abstractions;

namespace OnlineLearningPlatform.Infrastructure.Storage;

/// <summary>Signed server-side Cloudinary uploads for lesson videos and assignment attachments.</summary>
public sealed class CloudinaryMediaAssetStorage(
    HttpClient httpClient,
    IOptions<CloudinaryOptions> options) : IMediaAssetStorage
{
    private readonly CloudinaryOptions _options = options.Value;

    public async Task<StoredMediaAsset> UploadAsync(
        MediaAssetResourceType resourceType,
        string folder,
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken,
        bool privateDelivery = false)
    {
        EnsureConfigured();
        var safeFileName = Path.GetFileName(fileName.Replace('\\', '/'));
        var extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        var publicId = $"{folder.Trim('/')}/{Guid.NewGuid():N}{(resourceType == MediaAssetResourceType.Raw ? extension : string.Empty)}";
        var signedParameters = new Dictionary<string, string>
        {
            ["public_id"] = publicId,
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (privateDelivery)
            signedParameters["type"] = "private";

        using var form = CreateSignedForm(signedParameters);
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", safeFileName);

        using var response = await httpClient.PostAsync(Endpoint(resourceType, "upload"), form, cancellationToken);
        using var document = await ReadSuccessfulJsonAsync(response, cancellationToken);
        var secureUrl = document.RootElement.GetProperty("secure_url").GetString();
        var returnedPublicId = document.RootElement.GetProperty("public_id").GetString();
        if (string.IsNullOrWhiteSpace(secureUrl) || string.IsNullOrWhiteSpace(returnedPublicId))
            throw new InvalidOperationException("Cloudinary returned an incomplete media upload result.");
        return new StoredMediaAsset(secureUrl, returnedPublicId);
    }

    public async Task DeleteAsync(
        MediaAssetResourceType resourceType,
        string publicId,
        CancellationToken cancellationToken,
        bool privateDelivery = false)
    {
        EnsureConfigured();
        var signedParameters = new Dictionary<string, string>
        {
            ["invalidate"] = "true",
            ["public_id"] = publicId,
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        if (privateDelivery)
            signedParameters["type"] = "private";
        var values = new Dictionary<string, string>(signedParameters)
        {
            ["api_key"] = _options.ApiKey,
            ["signature"] = CreateSignature(signedParameters),
        };
        using var form = new FormUrlEncodedContent(values);
        using var response = await httpClient.PostAsync(Endpoint(resourceType, "destroy"), form, cancellationToken);
        using var document = await ReadSuccessfulJsonAsync(response, cancellationToken);
        if (document.RootElement.TryGetProperty("result", out var result) &&
            result.GetString() is not ("ok" or "not found"))
            throw new InvalidOperationException("Cloudinary could not remove the media asset.");
    }

    public string CreateTemporaryDownloadUrl(
        MediaAssetResourceType resourceType,
        string publicId,
        string format,
        TimeSpan lifetime)
    {
        EnsureConfigured();
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromHours(1))
            throw new ArgumentOutOfRangeException(nameof(lifetime));

        var now = DateTimeOffset.UtcNow;
        var signedParameters = new Dictionary<string, string>
        {
            ["attachment"] = "true",
            ["expires_at"] = now.Add(lifetime).ToUnixTimeSeconds()
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["format"] = format.TrimStart('.'),
            ["public_id"] = publicId,
            ["timestamp"] = now.ToUnixTimeSeconds()
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
        };
        var query = new Dictionary<string, string>(signedParameters)
        {
            ["api_key"] = _options.ApiKey,
            ["signature"] = CreateSignature(signedParameters),
        };
        var queryString = string.Join("&", query
            .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
            .Select(parameter => $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
        return $"{Endpoint(resourceType, "download")}?{queryString}";
    }

    private MultipartFormDataContent CreateSignedForm(IReadOnlyDictionary<string, string> signedParameters)
    {
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in signedParameters)
            form.Add(new StringContent(value), key);
        form.Add(new StringContent(_options.ApiKey), "api_key");
        form.Add(new StringContent(CreateSignature(signedParameters)), "signature");
        return form;
    }

    private string CreateSignature(IReadOnlyDictionary<string, string> parameters)
    {
        var payload = string.Join("&", parameters
            .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
            .Select(parameter => $"{parameter.Key}={parameter.Value}")) + _options.ApiSecret;
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    private Uri Endpoint(MediaAssetResourceType resourceType, string action)
    {
        var cloud = Uri.EscapeDataString(_options.CloudName);
        var resource = resourceType == MediaAssetResourceType.Video ? "video" : "raw";
        return new Uri($"https://api.cloudinary.com/v1_1/{cloud}/{resource}/{action}");
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
            throw new MediaAssetStorageNotConfiguredException();
    }

    private static async Task<JsonDocument> ReadSuccessfulJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(
                $"Cloudinary rejected the media request (HTTP {(int)response.StatusCode}).",
                inner: null,
                response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(body);
    }
}
