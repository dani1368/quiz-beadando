PRAGMA foreign_keys = OFF;

-- DROP TABLES in dependency order (most dependent FIRST)
DROP TABLE IF EXISTS QuizSessionAnswer;
DROP TABLE IF EXISTS QuizSessionQuestion;
DROP TABLE IF EXISTS QuizSession;
DROP TABLE IF EXISTS Answer;
DROP TABLE IF EXISTS QuestionImage;
DROP TABLE IF EXISTS Role2User;
DROP TABLE IF EXISTS Session;
DROP TABLE IF EXISTS Role;
DROP TABLE IF EXISTS Question;
DROP TABLE IF EXISTS User;

PRAGMA foreign_keys = ON;

-- User
CREATE TABLE User (
    UserID INTEGER NOT NULL PRIMARY KEY,
    Email TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    PasswordSalt TEXT NOT NULL
);

-- Session
CREATE TABLE Session (
    SessionID INTEGER NOT NULL PRIMARY KEY,
    SessionCookie TEXT NOT NULL UNIQUE,
    UserID INTEGER NOT NULL,
    ValidUntil INTEGER NOT NULL,
    LoginTime INTEGER NOT NULL,
    FOREIGN KEY (UserID) REFERENCES User(UserID)
);

-- Question
CREATE TABLE Question (
    QuestionID INTEGER PRIMARY KEY AUTOINCREMENT,
    Text TEXT NOT NULL
);

CREATE TABLE QuestionImage (
    ImageID INTEGER PRIMARY KEY AUTOINCREMENT,
    QuestionID INTEGER NOT NULL,
    ImageUrl TEXT NOT NULL,
    FOREIGN KEY (QuestionID) REFERENCES Question(QuestionID)
);

-- Answer
CREATE TABLE Answer (
    AnswerID INTEGER PRIMARY KEY AUTOINCREMENT,
    QuestionID INTEGER NOT NULL,
    Text TEXT NOT NULL,
    IsCorrect INTEGER NOT NULL,
    FOREIGN KEY (QuestionID) REFERENCES Question(QuestionID)
);

-- Quiz
CREATE TABLE QuizSession (
  SessionID INTEGER PRIMARY KEY AUTOINCREMENT,
  UserID INTEGER NOT NULL,
  StartTime INTEGER NOT NULL,
  EndTime INTEGER NOT NULL,
  DurationSeconds INTEGER NOT NULL,
  Score INTEGER NOT NULL DEFAULT -1,
  CreatedAt INTEGER DEFAULT (strftime('%s', 'now')),
  FOREIGN KEY(UserID) REFERENCES User(UserID)
);

CREATE TABLE QuizSessionQuestion (
  SessionID INTEGER NOT NULL,
  QuestionIndex INTEGER NOT NULL,
  QuestionID INTEGER NOT NULL,
  AnswerOrder TEXT NOT NULL, -- JSON array pl. "[3,2,1]"
  PRIMARY KEY (SessionID, QuestionIndex),
  FOREIGN KEY (SessionID) REFERENCES QuizSession(SessionID),
  FOREIGN KEY (QuestionID) REFERENCES Question(QuestionID)
);

CREATE TABLE QuizSessionAnswer (
  SessionID INTEGER NOT NULL,
  QuestionID INTEGER NOT NULL,
  SelectedAnswerID INTEGER NOT NULL,
  PRIMARY KEY (SessionID, QuestionID),
  FOREIGN KEY (SessionID) REFERENCES QuizSession(SessionID),
  FOREIGN KEY (QuestionID) REFERENCES Question(QuestionID),
  FOREIGN KEY (SelectedAnswerID) REFERENCES Answer(AnswerID)
);

-- Minta kérdések
INSERT INTO Question (Text) VALUES
('Milyen színű a stoptábla?'),
('Melyik jármű haladhat tovább elsőként a kereszteződésben?'),
('Mi a teendő, ha STOP táblát látsz?'),
('Mit jelent a kék alapon fehér nyíl jobbra?'),
('Kell-e indexelni körforgalomba behajtáskor?'),
('Mit jelent a STOP tábla?'),
('Mit jelent a ''Behajtani tilos'' tábla?'),
('Mit jelez az útépítés tábla?'),
('Milyen jelentése van a közlekedési lámpának?'),
('Mit jelez a gyalogosátkelő tábla?'),
('Melyik képen látható STOP és Behajtani tilos tábla?'),
('Melyik képek utalnak veszélyre?'),
('Melyik képek segítenek a gyalogosforgalom kezelésében?'),
('Mikor kell tompított fényszórót használni?'),
('Mire figyelmeztet a sárga villogó?'),
('Mikor van elsőbbséged gyalogosokkal szemben?'),
('Hol tilos előzni?'),
('Milyen sebességkorlátozás van lakott területen belül?'),
('Mikor használható a vészvillogó?'),
('Mi a teendő, ha megkülönböztető jelzést használó jármű közeledik?');

INSERT INTO QuestionImage (QuestionID, ImageUrl) VALUES
(6, 'https://openmoji.org/data/color/svg/1F6D1.svg'),
(7, 'https://openmoji.org/data/color/svg/26D4.svg'),
(8, 'https://openmoji.org/data/color/svg/1F6A7.svg'),
(9, 'https://openmoji.org/data/color/svg/1F6A6.svg'),
(10, 'https://openmoji.org/data/color/svg/1F6B6.svg'),
(11, 'https://openmoji.org/data/color/svg/1F6D1.svg'),
(11, 'https://openmoji.org/data/color/svg/26D4.svg'),
(12, 'https://openmoji.org/data/color/svg/1F6A6.svg'),
(12, 'https://openmoji.org/data/color/svg/1F6A7.svg'),
(13, 'https://openmoji.org/data/color/svg/1F6B6.svg'),
(13, 'https://openmoji.org/data/color/svg/1F6A6.svg');

INSERT INTO Answer (QuestionID, Text, IsCorrect) VALUES
(1, 'Piros-fehér', 1),
(1, 'Zöld-fehér', 0),
(1, 'Sárga', 0),
(2, 'Aki jobbról jön', 1),
(2, 'Aki balról jön', 0),
(2, 'Mindegyik egyszerre', 0),
(3, 'Mindig meg kell állni', 1),
(3, 'Meg kell állni, ha jön valaki', 0),
(3, 'Csak lassítani kell', 0),
(4, 'Kötelező haladási irány jobbra', 1),
(4, 'Jobbra kanyarodni tilos', 0),
(4, 'Veszélyes kanyar jobbra', 0),
(5, 'Igen, mindig kell indexelni', 1),
(5, 'Nem kell indexelni', 0),
(5, 'Csak balra indexelünk', 0),
(6, 'Meg kell állni', 1),
(6, 'Tilos megállni', 0),
(6, 'Kijelölt parkoló', 0),
(7, 'Tilos behajtani', 1),
(7, 'Kötelező jobbra', 0),
(7, 'Elsőbbségadás', 0),
(8, 'Útépítés van', 1),
(8, 'Körforgalom következik', 0),
(8, 'Főútvonal kezdete', 0),
(9, 'Forgalomirányító jelzőlámpa', 1),
(9, 'Gyalogos átkelőhely', 0),
(9, 'Parkolási zóna', 0),
(10, 'Gyalogosátkelőhely', 1),
(10, 'Zebra nélkül', 0),
(10, 'Kerékpárút', 0),
(11, 'Mindkettő', 1),
(11, 'Csak STOP', 0),
(11, 'Csak Behajtani tilos', 0),
(12, 'Lámpa és útépítés veszélyt jelez', 1),
(12, 'Behajtani tilos', 0),
(12, 'Lakott terület vége', 0),
(13, 'Lámpa és zebra segíti a gyalogosokat', 1),
(13, 'Sebességmérő', 0),
(13, 'Kijelölt buszmegálló', 0),
(14, 'Sötétedés után vagy rossz látásnál', 1),
(14, 'Bármikor', 0),
(14, 'Csak nappal', 0),
(15, 'Veszélyes helyre figyelmeztet', 1),
(15, 'Zöldhullám', 0),
(15, 'STOP után következik', 0),
(16, 'Kijelölt átkelőnél', 1),
(16, 'Bárhol', 0),
(16, 'Sosem', 0),
(17, 'Záróvonalnál és kanyarban', 1),
(17, 'Autópályán', 0),
(17, 'Lassító sávban', 0),
(18, '50 km/h', 1),
(18, '30 km/h', 0),
(18, '70 km/h', 0),
(19, 'Vészhelyzetben', 1),
(19, 'Ha fáradt vagy', 0),
(19, 'Ha áll a sor', 0),
(20, 'Szabad utat kell biztosítani', 1),
(20, 'Kikerülheted', 0),
(20, 'Ignorálható', 0);