using Microsoft.Extensions.Logging;
using Nesco.SignalRCommunicator.Core.Interfaces;
using System.Net.Http.Headers;

namespace Nesco.SignalRCommunicator.Client.Services;

/// <summary>
/// Default implementation of <see cref="IFileUploadService"/> that uploads files via HTTP.
/// </summary>
public class DefaultFileUploadService : IFileUploadService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DefaultFileUploadService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultFileUploadService"/> class.
    /// </summary>
    public DefaultFileUploadService(
        HttpClient httpClient,
        ILogger<DefaultFileUploadService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<string> UploadFileAsync(byte[] fileData, string fileName, string folder = "signalr-temp")
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var fileContent = new ByteArrayContent(fileData);
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync($"api/FileUpload/{folder}", content);
            if (response.IsSuccessStatusCode)
            {
                var filePath = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("File uploaded successfully: {FilePath}", filePath);
                return filePath.Trim('"'); // Remove quotes from JSON response
            }
            else
            {
                _logger.LogError("File upload failed with status: {StatusCode}", response.StatusCode);
                throw new Exception($"File upload failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<string> UploadStreamAsync(Stream stream, string fileName, string folder = "signalr-temp")
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/octet-stream");
            content.Add(streamContent, "file", fileName);

            var response = await _httpClient.PostAsync($"api/FileUpload/{folder}", content);
            if (response.IsSuccessStatusCode)
            {
                var filePath = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("Stream uploaded successfully: {FilePath}", filePath);
                return filePath.Trim('"');
            }
            else
            {
                _logger.LogError("Stream upload failed with status: {StatusCode}", response.StatusCode);
                throw new Exception($"Stream upload failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading stream");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteFileAsync(string filePath)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"api/FileUpload?path={Uri.EscapeDataString(filePath)}");
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FilePath}", filePath);
            return false;
        }
    }
}
