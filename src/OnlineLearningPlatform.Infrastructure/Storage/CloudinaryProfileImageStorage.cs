using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using OnlineLearningPlatform.Application.Abstractions;

namespace OnlineLearningPlatform.Infrastructure.Storage;

public sealed class CloudinaryProfileImageStorage(
    HttpClient httpClient,
    IOptions<CloudinaryOptions> options) : IProfileImageStorage
{
    private readonly CloudinaryOptions _options = options.Value;

    public async Task<StoredProfileImage> UploadAsync(
        Guid userId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var publicId = $"nikladori/profiles/{userId}/{Guid.NewGuid():N}";
        var signedParameters = new Dictionary<string, string>
        {
            ["public_id"] = publicId,
            ["timestamp"] = timestamp,
        };

        using var form = CreateSignedForm(signedParameters);
        var imageContent = new StreamContent(content);
        imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        var safeFileName = Path.GetFileName(fileName.Replace('\\', '/'));
        form.Add(imageContent, "file", safeFileName);

        using var response = await httpClient.PostAsync(Endpoint("upload"), form, cancellationToken);
        using var document = await ReadSuccessfulJsonAsync(response, cancellationToken);
        var root = document.RootElement;
        var secureUrl = root.GetProperty("secure_url").GetString();
        var returnedPublicId = root.GetProperty("public_id").GetString();

        if (string.IsNullOrWhiteSpace(secureUrl) || string.IsNullOrWhiteSpace(returnedPublicId))
        {
            throw new InvalidOperationException("Cloudinary returned an incomplete profile image result.");
        }

        return new StoredProfileImage(CreateOptimizedDeliveryUrl(secureUrl), returnedPublicId);
    }

    public async Task DeleteAsync(string publicId, CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var signedParameters = new Dictionary<string, string>
        {
            ["invalidate"] = "true",
            ["public_id"] = publicId,
            ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                .ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        var formValues = new Dictionary<string, string>(signedParameters)
        {
            ["api_key"] = _options.ApiKey,
            ["signature"] = CreateSignature(signedParameters),
        };
        using var form = new FormUrlEncodedContent(formValues);
        using var response = await httpClient.PostAsync(Endpoint("destroy"), form, cancellationToken);
        using var document = await ReadSuccessfulJsonAsync(response, cancellationToken);
        if (document.RootElement.TryGetProperty("result", out var result) &&
            result.GetString() is not ("ok" or "not found"))
        {
            throw new InvalidOperationException("Cloudinary could not remove the profile image.");
        }
    }

    private MultipartFormDataContent CreateSignedForm(IReadOnlyDictionary<string, string> signedParameters)
    {
        var form = new MultipartFormDataContent();
        foreach (var (key, value) in signedParameters)
        {
            form.Add(new StringContent(value), key);
        }

        form.Add(new StringContent(_options.ApiKey), "api_key");
        form.Add(new StringContent(CreateSignature(signedParameters)), "signature");
        return form;
    }

    private string CreateSignature(IReadOnlyDictionary<string, string> parameters)
    {
        var payload = string.Join("&", parameters
            .OrderBy(parameter => parameter.Key, StringComparer.Ordinal)
            .Select(parameter => $"{parameter.Key}={parameter.Value}")) + _options.ApiSecret;
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private Uri Endpoint(string action) => new(
        $"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(_options.CloudName)}/image/{action}");

    private static string CreateOptimizedDeliveryUrl(string secureUrl)
    {
        const string uploadPath = "/image/upload/";
        var uploadIndex = secureUrl.IndexOf(uploadPath, StringComparison.OrdinalIgnoreCase);
        if (uploadIndex < 0) return secureUrl;

        var insertionPoint = uploadIndex + uploadPath.Length;
        return secureUrl.Insert(insertionPoint, "f_auto/q_auto/c_fill,h_512,w_512/");
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
        {
            throw new ProfileImageStorageNotConfiguredException();
        }
    }

    private static async Task<JsonDocument> ReadSuccessfulJsonAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Cloudinary rejected the profile image request (HTTP {(int)response.StatusCode}).",
                inner: null,
                response.StatusCode);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(body);
    }
}
