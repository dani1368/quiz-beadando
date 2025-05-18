using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Backend.Controllers
{
    public class QuizQuestion
    {
        public int id { get; set; }
        public string text { get; set; }
        public string? imageUrl { get; set; }
        public List<QuizAnswer> answers { get; set; } = new();
    }

    public class QuizAnswer
    {
        public int id { get; set; }
        public string text { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class QuizController : ControllerBase
    {
        private const string ConnectionString = "Data Source=app.db";

        [HttpGet("start")]
        public IActionResult StartQuiz()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT q.QuestionID, q.Text, q.ImageUrl,
                       a.AnswerID, a.Text as AnswerText
                FROM Question q
                JOIN Answer a ON a.QuestionID = q.QuestionID
                ORDER BY RANDOM()
                LIMIT 40
            ";

            var reader = cmd.ExecuteReader();
            var questionMap = new Dictionary<int, QuizQuestion>();

            while (reader.Read())
            {
                int qid = reader.GetInt32(0);
                if (!questionMap.ContainsKey(qid))
                {
                    questionMap[qid] = new QuizQuestion
                    {
                        id = qid,
                        text = reader.GetString(1),
                        imageUrl = reader.IsDBNull(2) ? null : reader.GetString(2),
                        answers = new List<QuizAnswer>()
                    };
                }
                questionMap[qid].answers.Add(new QuizAnswer
                {
                    id = reader.GetInt32(3),
                    text = reader.GetString(4)
                });
            }

            var random = new Random();
            var selected = questionMap.Values.OrderBy(_ => random.Next()).Take(10).ToList();

            foreach (var q in selected)
            {
                q.answers = q.answers.OrderBy(_ => random.Next()).ToList();
            }

            return Ok(new { questions = selected });
        }

        [HttpPost("submit")]
        public IActionResult SubmitQuiz()
        {
            var form = Request.Form;
            var selectedAnswers = new Dictionary<int, int>();

            foreach (var key in form.Keys)
            {
                if (key.StartsWith("q") && int.TryParse(key.Substring(1), out int qid))
                {
                    int aid = int.Parse(form[key]);
                    selectedAnswers[qid] = aid;
                }
            }

            int correctCount = 0;

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var cookie = Request.Cookies["sessionid"];
            if (string.IsNullOrEmpty(cookie))
                return Unauthorized(new { success = false, message = "Nincs bejelentkezve." });

            var getUserCmd = connection.CreateCommand();
            getUserCmd.CommandText = "SELECT UserID FROM Session WHERE SessionCookie = $cookie";
            getUserCmd.Parameters.AddWithValue("$cookie", cookie);

            var userIdObj = getUserCmd.ExecuteScalar();
            if (userIdObj == null)
                return Unauthorized(new { success = false, message = "Érvénytelen munkamenet." });

            int userId = Convert.ToInt32(userIdObj);

            foreach (var pair in selectedAnswers)
            {
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT IsCorrect FROM Answer WHERE AnswerID = $aid";
                checkCmd.Parameters.AddWithValue("$aid", pair.Value);
                var result = checkCmd.ExecuteScalar();
                if (result != null && Convert.ToInt32(result) == 1)
                    correctCount++;
            }

            var quizCmd = connection.CreateCommand();
            quizCmd.CommandText = @"
                INSERT INTO Quiz (UserID, CreatedAt, Score)
                VALUES ($userId, strftime('%s','now'), $score);
                SELECT last_insert_rowid();
            ";
            quizCmd.Parameters.AddWithValue("$userId", userId);
            quizCmd.Parameters.AddWithValue("$score", correctCount);
            var quizId = (long)quizCmd.ExecuteScalar();

            foreach (var pair in selectedAnswers)
            {
                var insertCmd = connection.CreateCommand();
                insertCmd.CommandText = @"
                    INSERT INTO QuizAnswer (QuizID, QuestionID, SelectedAnswerID)
                    VALUES ($quizId, $qid, $aid)
                ";
                insertCmd.Parameters.AddWithValue("$quizId", quizId);
                insertCmd.Parameters.AddWithValue("$qid", pair.Key);
                insertCmd.Parameters.AddWithValue("$aid", pair.Value);
                insertCmd.ExecuteNonQuery();
            }

            return Ok(new { score = correctCount, total = selectedAnswers.Count });
        }

        [HttpGet("history")]
        public IActionResult GetQuizHistory()
        {
            var cookie = Request.Cookies["sessionid"];
            if (string.IsNullOrEmpty(cookie))
                return Unauthorized(new { success = false, message = "Nincs bejelentkezve." });

            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            var getUserCmd = connection.CreateCommand();
            getUserCmd.CommandText = "SELECT UserID FROM Session WHERE SessionCookie = $cookie";
            getUserCmd.Parameters.AddWithValue("$cookie", cookie);

            var userIdObj = getUserCmd.ExecuteScalar();
            if (userIdObj == null)
                return Unauthorized(new { success = false, message = "Érvénytelen munkamenet." });

            int userId = Convert.ToInt32(userIdObj);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT CreatedAt, Score FROM Quiz
                WHERE UserID = $userId
                ORDER BY CreatedAt DESC
            ";
            cmd.Parameters.AddWithValue("$userId", userId);

            var reader = cmd.ExecuteReader();
            var list = new List<object>();

            while (reader.Read())
            {
                list.Add(new
                {
                    timestamp = reader.GetInt64(0),
                    score = reader.GetInt32(1)
                });
            }

            return Ok(new { success = true, quizzes = list });
        }
    }
}
