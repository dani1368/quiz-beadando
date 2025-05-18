using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionController : ControllerBase
    {
        private const string DbPath = "Data Source=app.db";

        [HttpPost("register")]
        public IActionResult Register([FromForm] string email, [FromForm] string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                return BadRequest(new { reason = "empty" });
            }
        
            var salt = "abc123";
            var hash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password + salt));
        
            using var connection = new SqliteConnection(DbPath);
            connection.Open();
        
            // Ellenőrizd, hogy már létezik-e az email
            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = "SELECT COUNT(*) FROM User WHERE Email = $email";
            checkCmd.Parameters.AddWithValue("$email", email);
            var exists = (long)checkCmd.ExecuteScalar();
        
            if (exists > 0)
            {
                return Conflict(new { reason = "exists" }); // HTTP 409 Conflict
            }
        
            // Új felhasználó beszúrása
            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = @"
                INSERT INTO User (Email, PasswordHash, PasswordSalt)
                VALUES ($email, $hash, $salt);
                SELECT last_insert_rowid();
            ";
            insertCmd.Parameters.AddWithValue("$email", email);
            insertCmd.Parameters.AddWithValue("$hash", hash);
            insertCmd.Parameters.AddWithValue("$salt", salt);
        
            long userId = (long)insertCmd.ExecuteScalar();
        
            return CreateSessionAndSetCookie(connection, userId);
        }

        [HttpPost("login")]
        public IActionResult Login([FromForm] string email, [FromForm] string password)
        {
            using var connection = new SqliteConnection(DbPath);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT UserID, PasswordHash, PasswordSalt FROM User WHERE Email = $email";
            cmd.Parameters.AddWithValue("$email", email);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read())
            {
                // email nincs
                return NotFound(new { reason = "email" });
            }

            long userId = reader.GetInt64(0);
            string hash = reader.GetString(1);
            string salt = reader.GetString(2);
            string inputHash = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password + salt));

            if (hash != inputHash)
            {
                // jelszó hibás
                return Unauthorized(new { reason = "password" });
            }

            return CreateSessionAndSetCookie(connection, userId);
        }

        [HttpPost("logout")]
        public IActionResult Logout()
        {
            if (!Request.Cookies.TryGetValue("sessionid", out var sessionCookie))
                return Ok(new { success = true });

            using var connection = new SqliteConnection(DbPath);
            connection.Open();

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
            if (!Request.Cookies.TryGetValue("sessionid", out var sessionCookie))
                return Ok(new { loggedIn = false });

            using var connection = new SqliteConnection(DbPath);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT UserID FROM Session
                WHERE SessionCookie = $cookie AND ValidUntil > strftime('%s', 'now')
            ";
            cmd.Parameters.AddWithValue("$cookie", sessionCookie);

            var userId = cmd.ExecuteScalar();
            if (userId == null)
                return Ok(new { loggedIn = false });

            return Ok(new { loggedIn = true, userId });
        }

        private IActionResult CreateSessionAndSetCookie(SqliteConnection connection, long userId)
        {
            string sessionCookie = Guid.NewGuid().ToString();
            long validUntil = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 3600;
            long loginTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Session (SessionCookie, UserID, ValidUntil, LoginTime)
                VALUES ($cookie, $userId, $validUntil, $loginTime)
            ";
            cmd.Parameters.AddWithValue("$cookie", sessionCookie);
            cmd.Parameters.AddWithValue("$userId", userId);
            cmd.Parameters.AddWithValue("$validUntil", validUntil);
            cmd.Parameters.AddWithValue("$loginTime", loginTime);
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
    }
}