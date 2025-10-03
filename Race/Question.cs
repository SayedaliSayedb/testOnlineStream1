namespace WebApplication1.Race
{
    public class Question
    {
        public int Id { get; set; }
        public string Text { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public int CorrectAnswer { get; set; }
        public int TimeLimit { get; set; } = 30;
        public string? ImageUrl { get; set; }
    }
    public class Participant
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ProfileImage { get; set; } = string.Empty;
        public int Hearts { get; set; } = 3;
        public bool IsEliminated { get; set; }
        public bool IsOnline { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime LastActivity { get; set; }
        public Dictionary<int, ParticipantAnswer> Answers { get; set; } = new();
    }
    public class ParticipantAnswer
    {
        public int QuestionId { get; set; }
        public int SelectedAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public DateTime AnsweredAt { get; set; }
        public TimeSpan ResponseTime { get; set; }
    }
    public class QuizSettings
    {
        public int InitialHearts { get; set; } = 3;
        public bool CommentsEnabled { get; set; } = true;
        public DateTime QuizStartTime { get; set; }
        public string QuizTitle { get; set; } = "مسابقه آنلاین";
        public string HostName { get; set; } = "مجری";
    }
    public class QuizData
    {
        public List<Question> Questions { get; set; } = new();
        public QuizSettings Settings { get; set; } = new();
    }
    public class QuizState
    {
        public bool IsQuizActive { get; set; }
        public int CurrentQuestionId { get; set; } = -1;
        public bool IsQuestionActive { get; set; }
        public DateTime QuestionStartTime { get; set; }
        public DateTime QuestionEndTime { get; set; }
        public List<string> ActiveParticipantIds { get; set; } = new();
        public List<string> EliminatedParticipantIds { get; set; } = new();
    }
    public class Message
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime SentAt { get; set; }
        public string? UserImage { get; set; }
    }
}
