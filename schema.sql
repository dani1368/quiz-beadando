PRAGMA foreign_keys = ON;

-- User
DROP TABLE IF EXISTS User;
CREATE TABLE User (
    UserID INTEGER NOT NULL PRIMARY KEY,
    Email TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    PasswordSalt TEXT NOT NULL
);

-- SESSION
DROP TABLE IF EXISTS Session;
CREATE TABLE Session (
    SessionID INTEGER NOT NULL PRIMARY KEY,
    SessionCookie TEXT NOT NULL UNIQUE,
    UserID INTEGER NOT NULL,
    ValidUntil INTEGER NOT NULL,
    LoginTime INTEGER NOT NULL,
    FOREIGN KEY (UserID) REFERENCES User(UserID)
);

-- Roles
DROP TABLE IF EXISTS Role;
CREATE TABLE Role (
    RoleID INTEGER NOT NULL PRIMARY KEY,
    RoleName TEXT NOT NULL
);

INSERT INTO Role (RoleID, RoleName) VALUES (0, 'admin');
INSERT INTO Role (RoleID, RoleName) VALUES (1, 'user');

-- Roles to user map
DROP TABLE IF EXISTS Role2User;
CREATE TABLE Role2User (
    UserID INTEGER NOT NULL,
    RoleID INTEGER NOT NULL,
    PRIMARY KEY (UserID, RoleID),
    FOREIGN KEY (UserID) REFERENCES User(UserID),
    FOREIGN KEY (RoleID) REFERENCES Role(RoleID)
);

-- Question
DROP TABLE IF EXISTS Question;
CREATE TABLE Question (
    QuestionID INTEGER PRIMARY KEY AUTOINCREMENT,
    Text TEXT NOT NULL,
    ImageUrl TEXT
);

-- Answer
DROP TABLE IF EXISTS Answer;
CREATE TABLE Answer (
    AnswerID INTEGER PRIMARY KEY AUTOINCREMENT,
    QuestionID INTEGER NOT NULL,
    Text TEXT NOT NULL,
    IsCorrect INTEGER NOT NULL, -- 1 = helyes, 0 = hibás
    FOREIGN KEY (QuestionID) REFERENCES Question(QuestionID)
);

-- Filled out Quiz
DROP TABLE IF EXISTS Quiz;
CREATE TABLE Quiz (
    QuizID INTEGER PRIMARY KEY AUTOINCREMENT,
    UserID INTEGER NOT NULL,
    CreatedAt INTEGER NOT NULL,
    Score INTEGER NOT NULL,
    FOREIGN KEY (UserID) REFERENCES User(UserID)
);

-- QuizAnswer
DROP TABLE IF EXISTS QuizAnswer;
CREATE TABLE QuizAnswer (
    QuizAnswerID INTEGER PRIMARY KEY AUTOINCREMENT,
    QuizID INTEGER NOT NULL,
    QuestionID INTEGER NOT NULL,
    SelectedAnswerID INTEGER NOT NULL,
    FOREIGN KEY (QuizID) REFERENCES Quiz(QuizID),
    FOREIGN KEY (QuestionID) REFERENCES Question(QuestionID),
    FOREIGN KEY (SelectedAnswerID) REFERENCES Answer(AnswerID)
);

-- Minta kérdések
INSERT INTO Question (Text, ImageUrl) VALUES
('Milyen színű a stoptábla?', NULL),
('Melyik jármű haladhat tovább elsőként a kereszteződésben?', NULL),
('Mi a teendő, ha STOP táblát látsz?', NULL),
('Mit jelent a kék alapon fehér nyíl jobbra?', NULL),
('Kell-e indexelni körforgalomba behajtáskor?', NULL);

-- Minta válaszok
INSERT INTO Answer (QuestionID, Text, IsCorrect) VALUES
(1, 'Piros-fehér', 1),
(1, 'Zöld-fehér', 0),
(1, 'Sárga', 0),

(2, 'Aki jobbról jön', 1),
(2, 'Aki balról jön', 0),
(2, 'Mindegyik egyszerre', 0),

(3, 'Meg kell állni, ha jön valaki', 0),
(3, 'Mindig meg kell állni', 1),
(3, 'Csak lassítani kell', 0),

(4, 'Kötelező haladási irány jobbra', 1),
(4, 'Jobbra kanyarodni tilos', 0),
(4, 'Veszélyes kanyar jobbra', 0),

(5, 'Nem kell indexelni', 0),
(5, 'Igen, mindig indexelni kell', 1),
(5, 'Csak balra indexelünk', 0);