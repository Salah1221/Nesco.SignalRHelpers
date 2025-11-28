namespace Nesco.SignalRCommunicator.Core.Models;

/// <summary>
/// Defines the type of response being returned from a SignalR method invocation.
/// </summary>
public enum SignalRResponseType
{
    /// <summary>
    /// Response contains JSON data directly in the JsonData property.
    /// Used for small to medium-sized responses that can be sent directly through SignalR.
    /// </summary>
    JsonObject,

    /// <summary>
    /// Response data was too large and has been uploaded to a file.
    /// The FilePath property contains the path to the uploaded file.
    /// </summary>
    FilePath,

    /// <summary>
    /// Method returned null or no data.
    /// </summary>
    Null,

    /// <summary>
    /// An error occurred during method execution.
    /// The ErrorMessage property contains details about the error.
    /// </summary>
    Error
}
