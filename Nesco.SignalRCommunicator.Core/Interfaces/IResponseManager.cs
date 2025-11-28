using Nesco.SignalRCommunicator.Core.Models;

namespace Nesco.SignalRCommunicator.Core.Interfaces;

/// <summary>
/// Manages pending requests and their completion.
/// Decouples the hub from the service implementation.
/// </summary>
public interface IResponseManager
{
    /// <summary>
    /// Registers a new pending request.
    /// </summary>
    void RegisterRequest(string requestId, TaskCompletionSource<SignalRResponse> tcs);

    /// <summary>
    /// Completes a pending request with the given response.
    /// </summary>
    /// <returns>True if the request was found and completed, false otherwise.</returns>
    bool CompleteRequest(string requestId, SignalRResponse response);

    /// <summary>
    /// Removes a pending request without completing it.
    /// </summary>
    void RemoveRequest(string requestId);
}
