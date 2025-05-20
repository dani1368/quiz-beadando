using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Backend.Utils;
using System.Text.Json;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QuizController : ControllerBase
{
    const int MaxQuestions = 10, QuizDuration = 300;

    [HttpGet("current")]
    public IActionResult GetCurrentQuiz()
    {
        using var conn = DatabaseHelper.OpenConnection();
        if (!DatabaseHelper.TryGetUserIdFromSession(Request, conn, out int userId, out var unauthorized))
            return unauthorized!;

        using var sessionCmd = conn.CreateCommand();
        sessionCmd.CommandText = @"SELECT SessionID, StartTime, EndTime, DurationSeconds FROM QuizSession WHERE UserID = $uid AND Score = -1 AND EndTime > strftime('%s','now') ORDER BY StartTime DESC LIMIT 1";
        sessionCmd.Parameters.AddWithValue("$uid", userId);
        using var reader = sessionCmd.ExecuteReader();
        if (!reader.Read())
            return NoContent();

        long sessionId = reader.GetInt64(0), startTime = reader.GetInt64(1);
        int duration = reader.GetInt32(3);
        return Ok(new { startTime, duration, questions = LoadSessionQuestions(conn, sessionId) });
    }

    [HttpGet("start")]
    public IActionResult StartQuiz()
    {
        using var conn = DatabaseHelper.OpenConnection();
        if (!DatabaseHelper.TryGetUserIdFromSession(Request, conn, out int userId, out var unauthorized))
            return unauthorized!;

        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM QuizSession WHERE UserID = $uid AND Score = -1 AND EndTime > strftime('%s','now')";
        checkCmd.Parameters.AddWithValue("$uid", userId);
        var existingCountObj = checkCmd.ExecuteScalar();
        if (existingCountObj is long count && count > 0)
            return Conflict(new { message = "Már fut egy aktív kvíz." });

        var rand = new Random();
        var questions = GetRandomQuestions(conn, MaxQuestions, out _);
        long start = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), end = start + QuizDuration;

        using var insertCmd = conn.CreateCommand();
        insertCmd.CommandText = @"INSERT INTO QuizSession (UserID, StartTime, EndTime, DurationSeconds, Score) VALUES ($uid, $start, $end, $dur, -1); SELECT last_insert_rowid();";
        insertCmd.Parameters.AddWithValue("$uid", userId);
        insertCmd.Parameters.AddWithValue("$start", start);
        insertCmd.Parameters.AddWithValue("$end", end);
        insertCmd.Parameters.AddWithValue("$dur", QuizDuration);
        var sessionId = insertCmd.ExecuteScalar();
        if (sessionId is not long sid)
            return StatusCode(500, new { message = "Nem sikerült létrehozni a kvíz session-t." });

        int index = 0;
        foreach (var q in questions)
        {
            var shuffledIds = new List<int>();
            foreach (var a in q.answers)
                shuffledIds.Add((int)a.id);
            shuffledIds = shuffledIds.OrderBy(_ => rand.Next()).ToList();
            var order = JsonSerializer.Serialize(shuffledIds);

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO QuizSessionQuestion (SessionID, QuestionIndex, QuestionID, AnswerOrder) VALUES ($sid, $i, $qid, $order)";
            cmd.Parameters.AddWithValue("$sid", sid);
            cmd.Parameters.AddWithValue("$i", index++);
            cmd.Parameters.AddWithValue("$qid", q.id);
            cmd.Parameters.AddWithValue("$order", order);
            cmd.ExecuteNonQuery();
        }

        var formattedQuestions = new List<object>();
        foreach (var q in questions)
        {
            var formattedAnswers = new List<object>();
            var shuffled = ((IEnumerable<dynamic>)q.answers).OrderBy(_ => rand.Next()).ToList();
            foreach (var a in shuffled)
                formattedAnswers.Add(new { id = a.id, text = a.text });
            formattedQuestions.Add(new { q.id, q.text, q.imageUrls, answers = formattedAnswers });
        }

        return Ok(new { startTime = start, duration = QuizDuration, questions = formattedQuestions });
    }

    [HttpPost("submit")]
    public IActionResult SubmitQuiz()
    {
        var selected = new Dictionary<int, int>();
        foreach (var kv in Request.Form)
        {
            if (kv.Key.StartsWith("q") && int.TryParse(kv.Key[1..], out int qid) && int.TryParse(kv.Value.FirstOrDefault(), out int aid))
                selected[qid] = aid;
        }

        using var conn = DatabaseHelper.OpenConnection();
        if (!DatabaseHelper.TryGetUserIdFromSession(Request, conn, out int userId, out var unauthorized))
            return unauthorized!;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT SessionID, EndTime FROM QuizSession WHERE UserID = $uid AND Score = -1 ORDER BY StartTime DESC LIMIT 1";
        cmd.Parameters.AddWithValue("$uid", userId);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return BadRequest(new { message = "Nincs aktív kvíz." });

        long sessionId = reader.GetInt64(0), end = reader.GetInt64(1), now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        bool expired = now > end;
        int correct = 0;

        foreach (var (qid, aid) in selected)
        {
            using var check = conn.CreateCommand();
            check.CommandText = "SELECT IsCorrect FROM Answer WHERE AnswerID = $aid";
            check.Parameters.AddWithValue("$aid", aid);
            var result = check.ExecuteScalar();
            if (result is long val && val == 1) correct++;

            using var ins = conn.CreateCommand();
            ins.CommandText = "INSERT INTO QuizSessionAnswer (SessionID, QuestionID, SelectedAnswerID) VALUES ($sid, $qid, $aid)";
            ins.Parameters.AddWithValue("$sid", sessionId);
            ins.Parameters.AddWithValue("$qid", qid);
            ins.Parameters.AddWithValue("$aid", aid);
            ins.ExecuteNonQuery();
        }

        using var update = conn.CreateCommand();
        update.CommandText = "UPDATE QuizSession SET Score = $score, EndTime = strftime('%s','now') WHERE SessionID = $sid";
        update.Parameters.AddWithValue("$score", correct);
        update.Parameters.AddWithValue("$sid", sessionId);
        update.ExecuteNonQuery();

        return Ok(new { message = expired ? "Lejárt az idő. Mentettük az eredményt." : "Kvíz beküldve!", score = correct, total = selected.Count, sessionId });
    }

    [HttpGet("view/{sessionId}")]
    public IActionResult ViewQuizById(long sessionId)
    {
        using var conn = DatabaseHelper.OpenConnection();
        if (!DatabaseHelper.TryGetUserIdFromSession(Request, conn, out int userId, out var unauthorized))
            return unauthorized!;

        using var check = conn.CreateCommand();
        check.CommandText = "SELECT Score FROM QuizSession WHERE SessionID = $sid AND UserID = $uid AND Score >= 0";
        check.Parameters.AddWithValue("$sid", sessionId);
        check.Parameters.AddWithValue("$uid", userId);
        var scoreObj = check.ExecuteScalar();
        if (scoreObj == null)
            return Unauthorized(new { message = "Ehhez a kvízhez nincs jogosultságod vagy még nincs befejezve." });

        int score = Convert.ToInt32(scoreObj);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT Q.QuestionID, Q.Text, A.AnswerID, A.Text, A.IsCorrect, QA.SelectedAnswerID FROM QuizSessionQuestion QQ JOIN Question Q ON QQ.QuestionID = Q.QuestionID JOIN Answer A ON A.QuestionID = Q.QuestionID LEFT JOIN (SELECT QuestionID, SelectedAnswerID FROM QuizSessionAnswer WHERE SessionID = $sid) QA ON QA.QuestionID = Q.QuestionID WHERE QQ.SessionID = $sid ORDER BY QQ.QuestionIndex, A.AnswerID";
        cmd.Parameters.AddWithValue("$sid", sessionId);

        var questions = new Dictionary<int, dynamic>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            int qid = r.GetInt32(0), aid = r.GetInt32(2);
            if (!questions.ContainsKey(qid))
                questions[qid] = new
                {
                    id = qid,
                    text = r.GetString(1),
                    imageUrls = LoadImages(conn, qid),
                    selectedAnswerId = r.IsDBNull(5) ? (int?)null : r.GetInt32(5),
                    answers = new List<object>()
                };
            ((List<object>)questions[qid].answers).Add(new { id = aid, text = r.GetString(3), isCorrect = r.GetInt32(4) == 1 });
        }

        return Ok(new { score, total = questions.Count, questions = questions.Values });
    }

    [HttpDelete("session")]
    public IActionResult CancelQuiz()
    {
        using var conn = DatabaseHelper.OpenConnection();
        if (!DatabaseHelper.TryGetUserIdFromSession(Request, conn, out int userId, out var unauthorized))
            return unauthorized!;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM QuizSession WHERE UserID = $uid AND Score = -1 AND EndTime > strftime('%s','now')";
        cmd.Parameters.AddWithValue("$uid", userId);
        cmd.ExecuteNonQuery();
        return Ok(new { success = true });
    }

    [HttpGet("history")]
    public IActionResult GetQuizHistory()
    {
        using var conn = DatabaseHelper.OpenConnection();
        if (!DatabaseHelper.TryGetUserIdFromSession(Request, conn, out int userId, out var unauthorized))
            return unauthorized!;

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT SessionID, Score, StartTime, EndTime FROM QuizSession WHERE UserID = $uid AND Score >= 0 ORDER BY StartTime DESC";
        cmd.Parameters.AddWithValue("$uid", userId);

        var list = new List<object>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            long sid = reader.GetInt64(0), start = reader.GetInt64(2), end = reader.GetInt64(3);
            list.Add(new { sessionId = sid, score = reader.GetInt32(1), total = 10, timestamp = start, duration = (int)(end - start) });
        }
        return Ok(new { success = true, quizzes = list });
    }

    private static List<dynamic> GetRandomQuestions(SqliteConnection conn, int count, out List<int> ids)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT QuestionID, Text FROM Question ORDER BY RANDOM() LIMIT $count";
        cmd.Parameters.AddWithValue("$count", count);
        using var r = cmd.ExecuteReader();

        var list = new List<dynamic>();
        ids = new();
        while (r.Read())
        {
            int id = r.GetInt32(0);
            ids.Add(id);
            list.Add(new
            {
                id,
                text = r.GetString(1),
                imageUrls = LoadImages(conn, id),
                answers = LoadAnswers(conn, id)
            });
        }
        return list;
    }

    private static List<dynamic> LoadAnswers(SqliteConnection conn, int qid)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT AnswerID, Text FROM Answer WHERE QuestionID = $qid";
        cmd.Parameters.AddWithValue("$qid", qid);

        var list = new List<dynamic>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new { id = r.GetInt32(0), text = r.GetString(1) });
        return list;
    }

    private static List<string> LoadImages(SqliteConnection conn, int qid)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT ImageUrl FROM QuestionImage WHERE QuestionID = $qid";
        cmd.Parameters.AddWithValue("$qid", qid);

        var list = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    private static List<dynamic> LoadSessionQuestions(SqliteConnection conn, long sid)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"SELECT Q.QuestionID, Q.Text, QQ.AnswerOrder FROM QuizSessionQuestion QQ JOIN Question Q ON QQ.QuestionID = Q.QuestionID WHERE QQ.SessionID = $sid ORDER BY QQ.QuestionIndex";
        cmd.Parameters.AddWithValue("$sid", sid);

        var list = new List<dynamic>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            int qid = r.GetInt32(0);
            var order = JsonSerializer.Deserialize<List<int>>(r.GetString(2)) ?? new();
            var answers = LoadAnswers(conn, qid).ToDictionary(a => (int)a.id);
            list.Add(new
            {
                id = qid,
                text = r.GetString(1),
                imageUrls = LoadImages(conn, qid),
                answers = order.Where(answers.ContainsKey).Select(i => answers[i]).ToList()
            });
        }
        return list;
    }
}