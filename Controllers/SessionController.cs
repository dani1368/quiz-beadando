using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Backend.Utils;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    [HttpPost("register")]
    public IActionResult Register([FromForm] string email, [FromForm] string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return BadRequest(new { reason = "empty" });

        using var conn = DatabaseHelper.OpenConnection();
        if (DatabaseHelper.UserExists(conn, email))
            return Conflict(new { reason = "exists" });

        var salt = PasswordManager.GenerateSalt();
        var hash = PasswordManager.GeneratePasswordHash(password, salt);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"INSERT INTO User (Email, PasswordHash, PasswordSalt) VALUES ($email, $hash, $salt); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$email", email);
        cmd.Parameters.AddWithValue("$hash", hash);
        cmd.Parameters.AddWithValue("$salt", salt);

        var idObj = cmd.ExecuteScalar();
        if (idObj is not long userId)
            return StatusCode(500, new { reason = "insert_failed" });

        return CreateSessionAndSetCookie(conn, userId);
    }

    [HttpPost("login")]
    public IActionResult Login([FromForm] string email, [FromForm] string password)
    {
        using var conn = DatabaseHelper.OpenConnection();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT UserID, PasswordHash, PasswordSalt FROM User WHERE Email = $email";
        cmd.Parameters.AddWithValue("$email", email);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return NotFound(new { reason = "email" });

        var userId = reader.GetInt64(0);
        var storedHash = reader.GetString(1);
        var storedSalt = reader.GetString(2);

        if (!PasswordManager.Verify(password, storedSalt, storedHash))
            return Unauthorized(new { reason = "password" });

        return CreateSessionAndSetCookie(conn, userId);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        if (!Request.Cookies.TryGetValue("sessionid", out var sessionCookie))
            return Ok(new { success = true });

        using var conn = DatabaseHelper.OpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM Session WHERE SessionCookie = $cookie";
        cmd.Parameters.AddWithValue("$cookie", sessionCookie);
        cmd.ExecuteNonQuery();

        Response.Cookies.Delete("sessionid");
        return Ok(new { success = true });
    }

    [HttpGet("check")]
    public IActionResult Check()
    {
        using var conn = DatabaseHelper.OpenConnection();
        if (!Request.Cookies.TryGetValue("sessionid", out var cookie))
            return Ok(new { loggedIn = false });

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT UserID, ValidUntil FROM Session WHERE SessionCookie = $cookie";
        cmd.Parameters.AddWithValue("$cookie", cookie);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return Ok(new { loggedIn = false });

        var userId = reader.GetInt64(0);
        var validUntil = reader.GetInt64(1);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (now > validUntil)
        {
            using var del = conn.CreateCommand();
            del.CommandText = "DELETE FROM Session WHERE SessionCookie = $cookie";
            del.Parameters.AddWithValue("$cookie", cookie);
            del.ExecuteNonQuery();
            Response.Cookies.Delete("sessionid");
            return Ok(new { loggedIn = false });
        }

        return Ok(new { loggedIn = true, userId });
    }

    private IActionResult CreateSessionAndSetCookie(SqliteConnection conn, long userId)
    {
        var cookie = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var validUntil = now + 3600;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Session (SessionCookie, UserID, ValidUntil, LoginTime) VALUES ($cookie, $uid, $validUntil, $login)";
        cmd.Parameters.AddWithValue("$cookie", cookie);
        cmd.Parameters.AddWithValue("$uid", userId);
        cmd.Parameters.AddWithValue("$validUntil", validUntil);
        cmd.Parameters.AddWithValue("$login", now);
        cmd.ExecuteNonQuery();

        Response.Cookies.Append("sessionid", cookie, new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(1)
        });

        return Ok(new { success = true });
    }
}
