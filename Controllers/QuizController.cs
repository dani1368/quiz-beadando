using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Primitives;
using Backend.Utils;
using System.Text.Json;

namespace Backend.Controllers
{
    public class QuizQuestion
    {
        public int id { get; set; }
        public string text { get; set; } = string.Empty;
        public List<string> imageUrls { get; set; } = new();
        public List<QuizAnswer> answers { get; set; } = new();
    }

    public class QuizAnswer
    {
        public int id { get; set; }
        public string text { get; set; } = string.Empty;
    }

    public class QuizQuestionView
    {
        public int id { get; set; }
        public string text { get; set; } = string.Empty;
        public List<string> imageUrls { get; set; } = new();
        public List<QuizAnswerView> answers { get; set; } = new();
        public int? selectedAnswerId { get; set; }
    }

    public class QuizAnswerView
    {
        public int id { get; set; }
        public string text { get; set; } = string.Empty;
        public bool isCorrect { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class QuizController : ControllerBase
    {
        private const int MaxQuestions = 10;
        private const int MaxDuration = 300;

        [HttpGet("current")]
        public IActionResult GetCurrentQuiz()
        {
            using var connection = DbUtils.OpenConnection();
            if (!DbUtils.TryGetUserIdFromSession(Request, connection, out int userId, out var unauthorized))
                return unauthorized!;

            var sessionCmd = connection.CreateCommand();
            sessionCmd.CommandText = @"
                SELECT SessionID, StartTime, EndTime, DurationSeconds, Score
                FROM QuizSession
                WHERE UserID = $uid AND Score = -1 AND EndTime > strftime('%s', 'now')
                ORDER BY StartTime DESC LIMIT 1";
            sessionCmd.Parameters.AddWithValue("$uid", userId);

            using var reader = sessionCmd.ExecuteReader();
            if (!reader.Read()) return NoContent();

            long sessionId = reader.GetInt64(0);
            long startTime = reader.GetInt64(1);
            int duration = reader.GetInt32(3);

            var questions = LoadSessionQuestions(connection, sessionId);
            return Ok(new { startTime, duration, questions });
        }

        [HttpGet("start")]
        public IActionResult StartQuiz()
        {
            using var connection = DbUtils.OpenConnection();
            if (!DbUtils.TryGetUserIdFromSession(Request, connection, out int userId, out var unauthorized))
                return unauthorized!;

            var existing = connection.CreateCommand();
            existing.CommandText = "SELECT COUNT(*) FROM QuizSession WHERE UserID = $uid AND Score = -1 AND EndTime > strftime('%s', 'now')";
            existing.Parameters.AddWithValue("$uid", userId);
            var existingCountObj = existing.ExecuteScalar();
            if (existingCountObj is long count && count > 0)
                return Conflict(new { message = "Már fut egy aktív kvíz." });

            var questions = GetRandomQuestions(connection, MaxQuestions, out var questionIds);
            var random = new Random();
            long startTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long endTime = startTime + MaxDuration;

            var sessionInsert = connection.CreateCommand();
            sessionInsert.CommandText = @"
                INSERT INTO QuizSession (UserID, StartTime, EndTime, DurationSeconds, Score)
                VALUES ($uid, $start, $end, $dur, -1);
                SELECT last_insert_rowid();";
            sessionInsert.Parameters.AddWithValue("$uid", userId);
            sessionInsert.Parameters.AddWithValue("$start", startTime);
            sessionInsert.Parameters.AddWithValue("$end", endTime);
            sessionInsert.Parameters.AddWithValue("$dur", MaxDuration);
            var sessionIdObj = sessionInsert.ExecuteScalar();
            if (sessionIdObj is not long sessionId)
                return StatusCode(500, new { message = "Nem sikerült létrehozni a kvíz session-t." });

            foreach (var (q, index) in questions.Select((q, i) => (q, i)))
            {
                var answers = q.answers.OrderBy(_ => random.Next()).Select(a => a.id).ToList();
                var json = JsonSerializer.Serialize(answers);
                var insert = connection.CreateCommand();
                insert.CommandText = @"
                    INSERT INTO QuizSessionQuestion (SessionID, QuestionIndex, QuestionID, AnswerOrder)
                    VALUES ($sid, $idx, $qid, $order);";
                insert.Parameters.AddWithValue("$sid", sessionId);
                insert.Parameters.AddWithValue("$idx", index);
                insert.Parameters.AddWithValue("$qid", q.id);
                insert.Parameters.AddWithValue("$order", json);
                insert.ExecuteNonQuery();
            }

            var responseQuestions = questions.Select(q => new QuizQuestion
            {
                id = q.id,
                text = q.text,
                imageUrls = q.imageUrls,
                answers = q.answers.OrderBy(_ => random.Next()).ToList()
            }).ToList();

            return Ok(new { startTime, duration = MaxDuration, questions = responseQuestions });
        }

        [HttpPost("submit")]
        public IActionResult SubmitQuiz()
        {
            var form = Request.Form;
            var selectedAnswers = new Dictionary<int, int>();

            foreach (var key in form.Keys)
            {
                if (key.StartsWith("q") && int.TryParse(key[1..], out int qid))
                {
                    StringValues value = form[key];
                    if (value.Count > 0 && int.TryParse(value[0], out int aid))
                        selectedAnswers[qid] = aid;
                }
            }

            using var connection = DbUtils.OpenConnection();
            if (!DbUtils.TryGetUserIdFromSession(Request, connection, out int userId, out var unauthorized))
                return unauthorized!;

            var sessionCmd = connection.CreateCommand();
            sessionCmd.CommandText = @"
                SELECT SessionID, StartTime, EndTime, Score
                FROM QuizSession
                WHERE UserID = $uid AND Score = -1
                ORDER BY StartTime DESC LIMIT 1";
            sessionCmd.Parameters.AddWithValue("$uid", userId);
            using var reader = sessionCmd.ExecuteReader();

            if (!reader.Read())
                return BadRequest(new { message = "Nincs aktív kvíz." });

            long sessionId = reader.GetInt64(0);
            long endTime = reader.GetInt64(2);
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool expired = currentTime > endTime;

            int correctCount = 0;
            foreach (var (qid, aid) in selectedAnswers)
            {
                var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "SELECT IsCorrect FROM Answer WHERE AnswerID = $aid";
                checkCmd.Parameters.AddWithValue("$aid", aid);
                var result = checkCmd.ExecuteScalar();
                if (result != null && Convert.ToInt32(result) == 1)
                    correctCount++;

                var insertAnswer = connection.CreateCommand();
                insertAnswer.CommandText = @"
                    INSERT INTO QuizSessionAnswer (SessionID, QuestionID, SelectedAnswerID)
                    VALUES ($sid, $qid, $aid);";
                insertAnswer.Parameters.AddWithValue("$sid", sessionId);
                insertAnswer.Parameters.AddWithValue("$qid", qid);
                insertAnswer.Parameters.AddWithValue("$aid", aid);
                insertAnswer.ExecuteNonQuery();
            }

            var update = connection.CreateCommand();
            update.CommandText = "UPDATE QuizSession SET Score = $score, EndTime = strftime('%s','now') WHERE SessionID = $sid";
            update.Parameters.AddWithValue("$score", correctCount);
            update.Parameters.AddWithValue("$sid", sessionId);
            update.ExecuteNonQuery();

            if (expired)
                return Ok(new { message = "Lejárt az idő. Mentettük az eredményt.", score = correctCount, total = selectedAnswers.Count, sessionId });

            if (selectedAnswers.Count == 0)
                return Ok(new { message = "A kvízt megszakítottad. Nincs válasz, 0 pont.", score = 0, total = 0, sessionId });

            return Ok(new { message = "Kvíz beküldve!", score = correctCount, total = selectedAnswers.Count, sessionId });
        }

        [HttpGet("view/{sessionId}")]
        public IActionResult ViewQuizById(long sessionId)
        {
            using var connection = DbUtils.OpenConnection();
            if (!DbUtils.TryGetUserIdFromSession(Request, connection, out int userId, out var unauthorized))
                return unauthorized!;

            var checkCmd = connection.CreateCommand();
            checkCmd.CommandText = @"
                SELECT Score FROM QuizSession
                WHERE SessionID = $sid AND UserID = $uid AND Score >= 0";
            checkCmd.Parameters.AddWithValue("$sid", sessionId);
            checkCmd.Parameters.AddWithValue("$uid", userId);

            var scoreObj = checkCmd.ExecuteScalar();
            if (scoreObj == null)
                return Unauthorized(new { message = "Ehhez a kvízhez nincs jogosultságod vagy még nincs befejezve." });

            int score = Convert.ToInt32(scoreObj);

            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT Q.QuestionID, Q.Text, A.AnswerID, A.Text, A.IsCorrect,
                       QA.SelectedAnswerID, QQ.AnswerOrder
                FROM QuizSessionQuestion QQ
                JOIN Question Q ON QQ.QuestionID = Q.QuestionID
                JOIN Answer A ON A.QuestionID = Q.QuestionID
                LEFT JOIN (
                    SELECT QuestionID, SelectedAnswerID
                    FROM QuizSessionAnswer
                    WHERE SessionID = $sid
                ) QA ON QA.QuestionID = Q.QuestionID
                WHERE QQ.SessionID = $sid
                ORDER BY QQ.QuestionIndex, A.AnswerID";
            cmd.Parameters.AddWithValue("$sid", sessionId);

            var questions = new Dictionary<int, QuizQuestionView>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                int qid = r.GetInt32(0);
                string qtext = r.GetString(1);
                int aid = r.GetInt32(2);
                string atext = r.GetString(3);
                bool isCorrect = r.GetInt32(4) == 1;
                int? selectedAid = r.IsDBNull(5) ? null : r.GetInt32(5);

                if (!questions.ContainsKey(qid))
                {
                    questions[qid] = new QuizQuestionView
                    {
                        id = qid,
                        text = qtext,
                        imageUrls = LoadImages(connection, qid),
                        answers = new List<QuizAnswerView>(),
                        selectedAnswerId = selectedAid
                    };
                }

                questions[qid].answers.Add(new QuizAnswerView
                {
                    id = aid,
                    text = atext,
                    isCorrect = isCorrect
                });
            }

            return Ok(new
            {
                score,
                total = questions.Count,
                questions = questions.Values.ToList()
            });
        }

        [HttpDelete("session")]
        public IActionResult CancelQuiz()
        {
            using var connection = DbUtils.OpenConnection();
            if (!DbUtils.TryGetUserIdFromSession(Request, connection, out int userId, out var unauthorized))
                return unauthorized!;

            var delete = connection.CreateCommand();
            delete.CommandText = @"
                DELETE FROM QuizSession WHERE UserID = $uid AND Score = -1 AND EndTime > strftime('%s', 'now')";
            delete.Parameters.AddWithValue("$uid", userId);
            delete.ExecuteNonQuery();

            return Ok(new { success = true });
        }

        [HttpGet("history")]
        public IActionResult GetQuizHistory()
        {
            using var connection = DbUtils.OpenConnection();
            if (!DbUtils.TryGetUserIdFromSession(Request, connection, out int userId, out var unauthorized))
                return unauthorized!;
        
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT SessionID, Score, DurationSeconds, StartTime, EndTime
                FROM QuizSession
                WHERE UserID = $uid AND Score >= 0
                ORDER BY StartTime DESC";
            cmd.Parameters.AddWithValue("$uid", userId);
        
            var quizzes = new List<object>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                long sessionId = reader.GetInt64(0);
                int score = reader.GetInt32(1);
                int total = 10; // vagy MaxQuestions ha globálisan van
                long startTime = reader.GetInt64(3);
                long endTime = reader.GetInt64(4);
                int duration = (int)(endTime - startTime);
        
                quizzes.Add(new
                {
                    sessionId,
                    score,
                    total,
                    timestamp = startTime,
                    duration
                });
            }
        
            return Ok(new { success = true, quizzes });
        }

        private static List<QuizQuestion> GetRandomQuestions(SqliteConnection conn, int count, out List<int> ids)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT QuestionID, Text FROM Question ORDER BY RANDOM() LIMIT $count";
            cmd.Parameters.AddWithValue("$count", count);

            using var reader = cmd.ExecuteReader();
            var list = new List<QuizQuestion>();
            ids = new List<int>();

            while (reader.Read())
            {
                int id = reader.GetInt32(0);
                list.Add(new QuizQuestion { id = id, text = reader.GetString(1), answers = LoadAnswers(conn, id), imageUrls = LoadImages(conn, id) });
                ids.Add(id);
            }

            return list;
        }

        private static List<QuizAnswer> LoadAnswers(SqliteConnection conn, int questionId)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT AnswerID, Text FROM Answer WHERE QuestionID = $qid";
            cmd.Parameters.AddWithValue("$qid", questionId);

            var list = new List<QuizAnswer>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new QuizAnswer
                {
                    id = reader.GetInt32(0),
                    text = reader.GetString(1)
                });
            }
            return list;
        }

        private static List<string> LoadImages(SqliteConnection conn, int questionId)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT ImageUrl FROM QuestionImage WHERE QuestionID = $qid";
            cmd.Parameters.AddWithValue("$qid", questionId);

            var list = new List<string>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                list.Add(reader.GetString(0));

            return list;
        }

        private static List<QuizQuestion> LoadSessionQuestions(SqliteConnection conn, long sessionId)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT Q.QuestionID, Q.Text, QQ.AnswerOrder
                FROM QuizSessionQuestion QQ
                JOIN Question Q ON QQ.QuestionID = Q.QuestionID
                WHERE QQ.SessionID = $sid
                ORDER BY QQ.QuestionIndex";
            cmd.Parameters.AddWithValue("$sid", sessionId);
        
            var questions = new List<QuizQuestion>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var qid = reader.GetInt32(0);
                var text = reader.GetString(1);
                var answerOrder = JsonSerializer.Deserialize<List<int>>(reader.GetString(2)) ?? new();
        
                var allAnswers = LoadAnswers(conn, qid);
                var answerMap = allAnswers.ToDictionary(a => a.id);
        
                var orderedAnswers = new List<QuizAnswer>();
                foreach (var id in answerOrder)
                {
                    if (answerMap.TryGetValue(id, out var answer) && answer != null)
                        orderedAnswers.Add(answer);
                }
        
                questions.Add(new QuizQuestion
                {
                    id = qid,
                    text = text,
                    answers = orderedAnswers,
                    imageUrls = LoadImages(conn, qid)
                });
            }
        
            return questions;
        }
    }
}
