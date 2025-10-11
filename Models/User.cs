using System.Text.Json.Serialization;

namespace WebApplication1.Models
{
    public class User
    {
        [JsonPropertyName("nationalCode")]
        public string NationalCode { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("profileImage")]
        public string ProfileImage { get; set; } = string.Empty;

        [JsonPropertyName("totalScore")]
        public int TotalScore { get; set; } = 0;

        [JsonPropertyName("currentHearts")]
        public int CurrentHearts { get; set; } = 3;

        [JsonPropertyName("contestScores")]
        public Dictionary<string, ContestScore> ContestScores { get; set; } = new();

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("lastActivity")]
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;
    }

    public class ContestScore
    {
        [JsonPropertyName("contestId")]
        public string ContestId { get; set; } = string.Empty;

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("hearts")]
        public int Hearts { get; set; }

        [JsonPropertyName("lastUpdated")]
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("correctAnswers")]
        public int CorrectAnswers { get; set; }

        [JsonPropertyName("wrongAnswers")]
        public int WrongAnswers { get; set; }
    }
}
