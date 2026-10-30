using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace OnlineStore.API.Controllers;

[Route("api/auth")]
[AllowAnonymous]
public class AuthController(IConfiguration configuration) : StoreControllerBase
{
    public record LoginRequest([Required, StringLength(100)] string Username, [Required, StringLength(1024)] string Password);

    [HttpGet("session")]
    public IActionResult Session() => Ok(new { authenticated = User.Identity?.IsAuthenticated == true, username = User.Identity?.Name });

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        if (Request.Headers["X-Store-Request"] != "1") return StatusCode(403);
        var password = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(password) || password.Length < 16)
            return Problem(statusCode: 503, detail: "Administrator sign-in has not been configured.");
        var username = configuration["Admin:Username"] ?? "admin";
        if (request.Username != username || !CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Password)), SHA256.HashData(Encoding.UTF8.GetBytes(password))))
            return Problem(statusCode: 401, detail: "Invalid username or password.");
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, username), new Claim(ClaimTypes.Role, "Admin")], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2) });
        return Ok(new { authenticated = true, username });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        if (Request.Headers["X-Store-Request"] != "1") return StatusCode(403);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
