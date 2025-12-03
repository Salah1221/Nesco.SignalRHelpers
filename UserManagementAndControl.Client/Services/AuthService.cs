using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.JSInterop;

namespace UserManagementAndControl.Client.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private string? _accessToken;
    private bool _initialized;

    private const string TokenKey = "authToken";
    private const string UserIdKey = "userId";
    private const string EmailKey = "userEmail";

    public event Action? AuthStateChanged;

    public ClaimsPrincipal CurrentUser => _currentUser;
    public bool IsAuthenticated => _currentUser.Identity?.IsAuthenticated ?? false;
    public string? AccessToken => _accessToken;
    public string? UserId => _currentUser.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    public AuthService(HttpClient httpClient, IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
    }

    public async Task<LoginResult> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new { email, password });

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (result != null)
                {
                    _accessToken = result.Token;
                    await SetUserAsync(result.UserId, result.Email);
                    
                    // Persist to localStorage
                    await SaveToLocalStorageAsync(result.Token, result.UserId, result.Email);
                    
                    return new LoginResult { Success = true };
                }
            }

            var error = await response.Content.ReadAsStringAsync();
            return new LoginResult { Success = false, Error = error };
        }
        catch (Exception ex)
        {
            return new LoginResult { Success = false, Error = ex.Message };
        }
    }

    public async Task LogoutAsync()
    {
        _accessToken = null;
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        
        // Clear from localStorage
        await ClearLocalStorageAsync();
        
        AuthStateChanged?.Invoke();
    }

    public async Task<bool> CheckAuthAsync()
    {
        // Initialize from localStorage on first check
        if (!_initialized)
        {
            await InitializeFromLocalStorageAsync();
            _initialized = true;
        }

        // For JWT, we don't check with server - token is stored client-side
        // If we have a token and user, we're authenticated
        return IsAuthenticated;
    }

    private async Task InitializeFromLocalStorageAsync()
    {
        try
        {
            var token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
            var userId = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", UserIdKey);
            var email = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", EmailKey);

            if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(email))
            {
                _accessToken = token;
                await SetUserAsync(userId, email);
            }
        }
        catch (Exception)
        {
            // If localStorage is not available or throws an error, just continue without persisted auth
        }
    }

    private async Task SaveToLocalStorageAsync(string? token, string userId, string email)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, token ?? "");
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", UserIdKey, userId);
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", EmailKey, email);
        }
        catch (Exception)
        {
            // If localStorage is not available, just continue without persistence
        }
    }

    private async Task ClearLocalStorageAsync()
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", UserIdKey);
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", EmailKey);
        }
        catch (Exception)
        {
            // If localStorage is not available, just continue
        }
    }

    private Task SetUserAsync(string userId, string email)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, email)
        };

        _currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
        AuthStateChanged?.Invoke();
        return Task.CompletedTask;
    }
}

public class LoginResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class LoginResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Token { get; set; }
}
