using System.Text.Json;

namespace WebApplication1.Race.Services
{
    // Services/IQuizDataService.cs
    public interface IQuizDataService
    {
        Task<QuizData> GetQuizDataAsync();
        Task SaveQuizDataAsync(QuizData quizData);
        Task<QuizSettings> GetQuizSettingsAsync();
        Task SaveQuizSettingsAsync(QuizSettings settings);
        Task<QuizState> GetQuizStateAsync();
        Task SaveQuizStateAsync(QuizState state);
        Task<List<Message>> GetMessagesAsync();
        Task SaveMessageAsync(Message message);
        Task ClearMessagesAsync();
    }

    public class JsonFileDataService : IQuizDataService
    {
        private readonly string _dataPath;
        private readonly JsonSerializerOptions _jsonOptions;

        public JsonFileDataService(IWebHostEnvironment environment)
        {
            _dataPath = Path.Combine(environment.ContentRootPath, "Data");
            Directory.CreateDirectory(_dataPath);

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
        }

        public async Task<QuizData> GetQuizDataAsync()
        {
            var filePath = Path.Combine(_dataPath, "quiz-data.json");
            if (!File.Exists(filePath))
                return new QuizData();

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<QuizData>(json, _jsonOptions) ?? new QuizData();
            }
            catch
            {
                return new QuizData();
            }
        }

        public async Task SaveQuizDataAsync(QuizData quizData)
        {
            var filePath = Path.Combine(_dataPath, "quiz-data.json");
            var json = JsonSerializer.Serialize(quizData, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task<QuizSettings> GetQuizSettingsAsync()
        {
            var filePath = Path.Combine(_dataPath, "quiz-settings.json");
            if (!File.Exists(filePath))
                return new QuizSettings();

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<QuizSettings>(json, _jsonOptions) ?? new QuizSettings();
            }
            catch
            {
                return new QuizSettings();
            }
        }

        public async Task SaveQuizSettingsAsync(QuizSettings settings)
        {
            var filePath = Path.Combine(_dataPath, "quiz-settings.json");
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task<QuizState> GetQuizStateAsync()
        {
            var filePath = Path.Combine(_dataPath, "quiz-state.json");
            if (!File.Exists(filePath))
                return new QuizState();

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<QuizState>(json, _jsonOptions) ?? new QuizState();
            }
            catch
            {
                return new QuizState();
            }
        }

        public async Task SaveQuizStateAsync(QuizState state)
        {
            var filePath = Path.Combine(_dataPath, "quiz-state.json");
            var json = JsonSerializer.Serialize(state, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task<List<Message>> GetMessagesAsync()
        {
            var filePath = Path.Combine(_dataPath, "messages.json");
            if (!File.Exists(filePath))
                return new List<Message>();

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<List<Message>>(json, _jsonOptions) ?? new List<Message>();
            }
            catch
            {
                return new List<Message>();
            }
        }

        public async Task SaveMessageAsync(Message message)
        {
            var messages = await GetMessagesAsync();
            messages.Add(message);

            // Keep only last 100 messages
            if (messages.Count > 100)
                messages = messages.Skip(messages.Count - 100).ToList();

            var filePath = Path.Combine(_dataPath, "messages.json");
            var json = JsonSerializer.Serialize(messages, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task ClearMessagesAsync()
        {
            var filePath = Path.Combine(_dataPath, "messages.json");
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
