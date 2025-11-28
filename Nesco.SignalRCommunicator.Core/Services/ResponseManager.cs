using System.Collections.Concurrent;
using Nesco.SignalRCommunicator.Core.Interfaces;
using Nesco.SignalRCommunicator.Core.Models;

namespace Nesco.SignalRCommunicator.Core.Services;

/// <summary>
/// Default implementation of IResponseManager.
/// Thread-safe manager for pending SignalR requests.
/// </summary>
public class ResponseManager : IResponseManager
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<SignalRResponse>> _pendingRequests = new();

    public void RegisterRequest(string requestId, TaskCompletionSource<SignalRResponse> tcs)
    {
        if (!_pendingRequests.TryAdd(requestId, tcs))
        {
            throw new InvalidOperationException($"Request {requestId} is already registered");
        }
    }

    public bool CompleteRequest(string requestId, SignalRResponse response)
    {
        if (_pendingRequests.TryRemove(requestId, out var tcs))
        {
            tcs.TrySetResult(response);
            return true;
        }
        return false;
    }

    public void RemoveRequest(string requestId)
    {
        _pendingRequests.TryRemove(requestId, out _);
    }
}
