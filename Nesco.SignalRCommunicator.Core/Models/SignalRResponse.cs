namespace Nesco.SignalRCommunicator.Core.Models;

/// <summary>
/// Represents a standardized response from a SignalR method invocation.
/// Supports multiple response types including direct JSON data, file paths for large data, null responses, and errors.
/// </summary>
public class SignalRResponse
{
    /// <summary>
    /// Gets or sets the type of response being returned.
    /// </summary>
    public SignalRResponseType ResponseType { get; set; }

    /// <summary>
    /// Gets or sets the JSON data for direct responses (when data is small enough).
    /// Used when ResponseType is JsonObject.
    /// </summary>
    public object? JsonData { get; set; }

    /// <summary>
    /// Gets or sets the file path where large response data has been uploaded.
    /// Used when ResponseType is FilePath.
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    /// Gets or sets the error message if an error occurred during method execution.
    /// Used when ResponseType is Error.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
