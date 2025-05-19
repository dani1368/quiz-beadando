using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System.Text;
using Backend.Utils;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : ControllerBase
    {
        private const string StaticSalt = "abc123";

        [HttpPost("register")]
        public IActionResult Register([FromForm] string email, [FromForm] string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                return BadRequest(new { reason = "empty" });

            using var connection = DbUtils.OpenConnection();

            if (DbUtils.UserExists(connection, email))
                return Conflict(new { reason = "exists" });

            string hash = HashPassword(password, StaticSalt);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO User (Email, PasswordHash, PasswordSalt)
                VALUES ($email, $hash, $salt);
                SELECT last_insert_rowid();
            ";
            cmd.Parameters.AddWithValue("$email", email);
            cmd.Parameters.AddWithValue("$hash", hash);
            cmd.Parameters.AddWithValue("$salt", StaticSalt);

            var result = cmd.ExecuteScalar();
            if (result == null || result == DBNull.Value)
                return StatusCode(500, new { reason = "insert_failed" });

            long userId = Convert.ToInt64(result);
            return CreateSessionAndSetCookie(connection, userId);
        }

        [HttpPost("login")]
        public IActionResult Login([FromForm] string email, [FromForm] string password)
        {
            using var connection = DbUtils.OpenConnection();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT UserID, PasswordHash, PasswordSalt FROM User WHERE Email = $email";
            cmd.Parameters.AddWithValue("$email", email);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
                return NotFound(new { reason = "email" });

            long userId = reader.GetInt64(0);
            string storedHash = reader.GetString(1);
            string storedSalt = reader.GetString(2);
            string inputHash = HashPassword(password, storedSalt);

            if (storedHash != inputHash)
                return Unauthorized(new { reason = "password" });

            return CreateSessionAndSetCookie(connection, userId);
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            if (!Request.Cookies.TryGetValue("sessionid", out var sessionCookie))
                return Ok(new { success = true });

            using var connection = DbUtils.OpenConnection();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM Session WHERE SessionCookie = $cookie";
            cmd.Parameters.AddWithValue("$cookie", sessionCookie);
            cmd.ExecuteNonQuery();

            Response.Cookies.Delete("sessionid");

            return Ok(new { success = true });
        }

        [HttpGet("check")]
        public IActionResult Check()
        {
            using var connection = DbUtils.OpenConnection();
            if (!DbUtils.TryGetUserIdFromSession(Request, connection, out int userId, out _))
                return Ok(new { loggedIn = false });

            return Ok(new { loggedIn = true, userId });
        }

        private IActionResult CreateSessionAndSetCookie(SqliteConnection connection, long userId)
        {
            string sessionCookie = Guid.NewGuid().ToString();
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long validUntil = now + 3600;

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Session (SessionCookie, UserID, ValidUntil, LoginTime)
                VALUES ($cookie, $userId, $validUntil, $loginTime)
            ";
            cmd.Parameters.AddWithValue("$cookie", sessionCookie);
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$validUntil", validUntil);
            cmd.Parameters.AddWithValue("$loginTime", now);
            cmd.ExecuteNonQuery();

            Response.Cookies.Append("sessionid", sessionCookie, new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            });

            return Ok(new { success = true });
        }

        private static string HashPassword(string password, string salt)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(password + salt));
        }
    }
}
