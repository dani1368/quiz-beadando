using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;

namespace Backend.Utils;

public static class DatabaseHelper
{
    private const string ConnectionString = "Data Source=app.db";

    public static SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(ConnectionString);
        conn.Open();
        return conn;
    }

    public static bool TryGetUserIdFromSession(HttpRequest request, SqliteConnection connection, out int userId, out IActionResult? unauthorizedResult)
    {
        userId = -1;
        unauthorizedResult = null;

        if (!request.Cookies.TryGetValue("sessionid", out var cookie))
        {
            unauthorizedResult = new UnauthorizedObjectResult(new { success = false, message = "Nincs bejelentkezve." });
            return false;
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT UserID, ValidUntil FROM Session WHERE SessionCookie = $cookie";
        cmd.Parameters.AddWithValue("$cookie", cookie);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
        {
            unauthorizedResult = new UnauthorizedObjectResult(new { success = false, message = "Érvénytelen munkamenet." });
            return false;
        }

        long validUntil = reader.GetInt64(1);
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > validUntil)
        {
            DeleteSession(connection, cookie);
            unauthorizedResult = new UnauthorizedObjectResult(new { success = false, message = "A munkamenet lejárt." });
            return false;
        }

        userId = reader.GetInt32(0);
        return true;
    }

    public static void DeleteSession(SqliteConnection connection, string cookie)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Session WHERE SessionCookie = $cookie";
        cmd.Parameters.AddWithValue("$cookie", cookie);
        cmd.ExecuteNonQuery();
    }

    public static bool UserExists(SqliteConnection connection, string email)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM User WHERE Email = $email";
        cmd.Parameters.AddWithValue("$email", email);
        var result = cmd.ExecuteScalar();
        return result != null && result != DBNull.Value && Convert.ToInt64(result) > 0;
    }
}
