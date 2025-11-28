using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BlazorServerApplication.Data;

namespace BlazorServerApplication.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AuthController(
        IConfiguration configuration,
        ILogger<AuthController> logger,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _configuration = configuration;
        _logger = logger;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { message = "Username and password are required" });
        }

        _logger.LogInformation("User {Username} attempting login", request.Username);

        // Find user by email or username
        var user = await _userManager.FindByEmailAsync(request.Username);
        if (user == null)
        {
            user = await _userManager.FindByNameAsync(request.Username);
        }

        if (user == null)
        {
            _logger.LogWarning("Login failed: User {Username} not found", request.Username);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        // Check password
        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: false);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Login failed: Invalid password for user {Username}", request.Username);
            return Unauthorized(new { message = "Invalid credentials" });
        }

        _logger.LogInformation("User {Username} ({Email}) logged in successfully", user.UserName, user.Email);

        // Generate JWT token with user ID
        var token = GenerateJwtToken(user.Id, user.UserName ?? user.Email ?? "Unknown", user.Email ?? "");

        return Ok(new LoginResponse
        {
            Token = token,
            Username = user.UserName ?? user.Email ?? "Unknown",
            Email = user.Email ?? "",
            ExpiresIn = 3600 // 1 hour
        });
    }

    [HttpPost("validate")]
    public IActionResult ValidateToken()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "Invalid token" });
        }

        return Ok(new { userId, username });
    }

    private string GenerateJwtToken(string userId, string username, string email)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? "DemoSecretKey1234567890ABCDEFGHIJKLMNOP"; // Minimum 32 characters
        var issuer = jwtSettings["Issuer"] ?? "BlazorServerApplication";
        var audience = jwtSettings["Audience"] ?? "BlazorWebassemblyApp";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Email, email),
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}
