using WebApplication1.Models;
using WebApplication1.Services.Interfaces;

namespace WebApplication1.Services
{
    public interface IQuizService
    {
        Task<QuizQuestion> CreateQuestionAsync(string question, List<string> options, int correctIndex, int timeLimit, string contestId);
        Task<QuizInfo> StartQuizAsync(QuizQuestion question, string streamId);
        Task<bool> SubmitAnswerAsync(string quizId, string connectionId, string nationalCode, int answerIndex);
        Task<QuizInfo> GetQuizAsync(string quizId);
        Task<List<QuizInfo>> GetActiveQuizzesAsync();
    }

    public class QuizService : IQuizService
    {
        private readonly IUserService _userService;
        private readonly ILogger<QuizService> _logger;
        private readonly Dictionary<string, QuizInfo> _activeQuizzes = new();

        public QuizService(IUserService userService, ILogger<QuizService> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        public Task<QuizQuestion> CreateQuestionAsync(string question, List<string> options, int correctIndex, int timeLimit, string contestId)
        {
            var quizQuestion = new QuizQuestion
            {
                Id = Guid.NewGuid().ToString(),
                Question = question,
                Options = options,
                CorrectAnswerIndex = correctIndex,
                TimeLimit = timeLimit,
                ContestId = contestId,
                CreatedAt = DateTime.UtcNow
            };

            return Task.FromResult(quizQuestion);
        }

        public Task<QuizInfo> StartQuizAsync(QuizQuestion question, string streamId)
        {
            var quizInfo = new QuizInfo
            {
                Id = Guid.NewGuid().ToString(),
                Question = question,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddSeconds(question.TimeLimit),
                Participants = new Dictionary<string, QuizAnswer>(),
                StreamId = streamId,
                IsActive = true
            };

            _activeQuizzes[quizInfo.Id] = quizInfo;

            // زمان‌بندی پایان مسابقه
            _ = Task.Delay(question.TimeLimit * 1000).ContinueWith(async _ =>
            {
                await EndQuizAsync(quizInfo.Id);
            });

            _logger.LogInformation("Quiz started: {QuizId} for stream: {StreamId}", quizInfo.Id, streamId);
            return Task.FromResult(quizInfo);
        }

        public async Task<bool> SubmitAnswerAsync(string quizId, string connectionId, string nationalCode, int answerIndex)
        {
            if (!_activeQuizzes.TryGetValue(quizId, out var quiz) || !quiz.IsActive)
                return false;

            // بررسی اینکه کاربر قبلاً پاسخ نداده باشد
            if (quiz.Participants.ContainsKey(connectionId))
                return false;

            var isCorrect = answerIndex == quiz.Question.CorrectAnswerIndex;
            var answerTime = DateTime.UtcNow;
            var responseTime = answerTime - quiz.StartTime;

            var quizAnswer = new QuizAnswer
            {
                UserConnectionId = connectionId,
                NationalCode = nationalCode,
                AnswerIndex = answerIndex,
                IsCorrect = isCorrect,
                AnswerTime = answerTime,
                ResponseTime = responseTime
            };

            quiz.Participants[connectionId] = quizAnswer;

            // به‌روزرسانی امتیاز کاربر
            var scoreChange = isCorrect ? 10 : 0;
            var heartsChange = isCorrect ? 0 : -1;

            await _userService.UpdateUserScoreAsync(nationalCode, quiz.Question.ContestId, scoreChange, heartsChange);

            _logger.LogInformation("Quiz answer submitted: User {NationalCode}, Correct: {IsCorrect}, ResponseTime: {ResponseTime}",
                nationalCode, isCorrect, responseTime);

            return true;
        }

        public Task<QuizInfo> GetQuizAsync(string quizId)
        {
            _activeQuizzes.TryGetValue(quizId, out var quiz);
            return Task.FromResult(quiz);
        }

        public Task<List<QuizInfo>> GetActiveQuizzesAsync()
        {
            var activeQuizzes = _activeQuizzes.Values
                .Where(q => q.IsActive)
                .ToList();

            return Task.FromResult(activeQuizzes);
        }

        private async Task EndQuizAsync(string quizId)
        {
            if (_activeQuizzes.TryGetValue(quizId, out var quiz))
            {
                quiz.IsActive = false;
                quiz.EndTime = DateTime.UtcNow;

                _logger.LogInformation("Quiz ended: {QuizId}, Total participants: {Participants}",
                    quizId, quiz.Participants.Count);

                // حذف مسابقه پس از 5 دقیقه
                _ = Task.Delay(TimeSpan.FromMinutes(5)).ContinueWith(_ =>
                {
                    _activeQuizzes.Remove(quizId);
                });
            }
        }
    }
}
