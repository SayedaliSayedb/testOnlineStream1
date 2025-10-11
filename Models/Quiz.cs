namespace WebApplication1.Models
{
    public class QuizQuestion
    {
        public string Id { get; set; } = string.Empty;
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public int CorrectAnswerIndex { get; set; }
        public int TimeLimit { get; set; } = 30;
        public string ContestId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class QuizInfo
    {
        public string Id { get; set; } = string.Empty;
        public QuizQuestion Question { get; set; } = new();
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public Dictionary<string, QuizAnswer> Participants { get; set; } = new();
        public string StreamId { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class QuizAnswer
    {
        public string UserConnectionId { get; set; } = string.Empty;
        public string NationalCode { get; set; } = string.Empty;
        public int AnswerIndex { get; set; }
        public bool IsCorrect { get; set; }
        public DateTime AnswerTime { get; set; }
        public TimeSpan ResponseTime { get; set; }
    }
}
