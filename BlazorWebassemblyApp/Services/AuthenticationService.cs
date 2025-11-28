using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace BlazorWebassemblyApp.Services;

public class AuthenticationService
{
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<AuthenticationService> _logger;
    private const string TokenKey = "authToken";
    private const string UsernameKey = "username";

    public event Action? OnAuthStateChanged;

    public AuthenticationService(
        HttpClient httpClient,
        IJSRuntime jsRuntime,
        ILogger<AuthenticationService> logger)
    {
        _httpClient = httpClient;
        _jsRuntime = jsRuntime;
        _logger = logger;
    }

    public async Task<bool> LoginAsync(string username, string password, string serverUrl)
    {
        try
        {
            var loginUrl = $"{serverUrl.TrimEnd('/')}/api/auth/login";
            _logger.LogInformation("Attempting login to {Url}", loginUrl);

            var response = await _httpClient.PostAsJsonAsync(loginUrl, new
            {
                Username = username,
                Password = password
            });

            if (response.IsSuccessStatusCode)
            {
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
                {
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenKey, loginResponse.Token);
                    await _jsRuntime.InvokeVoidAsync("localStorage.setItem", UsernameKey, loginResponse.Username);

                    _logger.LogInformation("Login successful for user {Username}", username);
                    OnAuthStateChanged?.Invoke();
                    return true;
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Login failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error for user {Username}", username);
            return false;
        }
    }

    public async Task LogoutAsync()
    {
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", UsernameKey);
        OnAuthStateChanged?.Invoke();
        _logger.LogInformation("User logged out");
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task<string?> GetUsernameAsync()
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", UsernameKey);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsAuthenticatedAsync()
    {
        var token = await GetTokenAsync();
        return !string.IsNullOrEmpty(token);
    }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}
