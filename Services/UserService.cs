using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json;
using WebApplication1.Models;
using WebApplication1.Services.Interfaces;

namespace WebApplication1.Services
{
    public class UserService : IUserService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;
        private readonly ILogger<UserService> _logger;

        public UserService(IConfiguration configuration, ILogger<UserService> logger)
        {
            var awsCredentials = new BasicAWSCredentials("mhpduk29h5inip2u", "3567524a-1844-4a7c-beb3-0c2c5c4e414c");
            var config = new AmazonS3Config { ServiceURL = "https://storage.c2.liara.space" };
            _s3Client = new AmazonS3Client(awsCredentials, config);
            _bucketName = configuration["AWS:BucketName"] ?? "quiz-contest-bucket";
            _logger = logger;
        }

        public async Task<User> GetOrCreateUserAsync(string nationalCode, string username, string profileImage)
        {
            try
            {
                // بررسی وجود کاربر
                var existingUser = await GetUserAsync(nationalCode);
                if (existingUser != null)
                {
                    existingUser.LastActivity = DateTime.UtcNow;
                    await SaveUserAsync(existingUser);
                    return existingUser;
                }

                // ایجاد کاربر جدید
                var newUser = new User
                {
                    NationalCode = nationalCode,
                    Username = username,
                    ProfileImage = profileImage,
                    CurrentHearts = 3,
                    TotalScore = 0,
                    ContestScores = new Dictionary<string, ContestScore>(),
                    CreatedAt = DateTime.UtcNow,
                    LastActivity = DateTime.UtcNow,
                    IsActive = true
                };

                await SaveUserAsync(newUser);
                _logger.LogInformation("User created: {NationalCode}", nationalCode);
                return newUser;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrCreateUserAsync for national code: {NationalCode}", nationalCode);
                throw;
            }
        }

        public async Task<User> GetUserAsync(string nationalCode)
        {
            try
            {
                var request = new GetObjectRequest
                {
                    BucketName = _bucketName,
                    Key = $"users/{nationalCode}.json"
                };

                using var response = await _s3Client.GetObjectAsync(request);
                using var stream = response.ResponseStream;
                using var reader = new StreamReader(stream);
                var json = await reader.ReadToEndAsync();

                return JsonSerializer.Deserialize<User>(json) ?? throw new Exception("User data is null");
            }
            catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user: {NationalCode}", nationalCode);
                return null;
            }
        }

        public async Task UpdateUserScoreAsync(string nationalCode, string contestId, int scoreChange, int heartsChange)
        {
            var user = await GetUserAsync(nationalCode);
            if (user == null) return;

            // به‌روزرسانی امتیاز کلی
            user.TotalScore += scoreChange;
            user.CurrentHearts += heartsChange;
            user.LastActivity = DateTime.UtcNow;

            // محدودیت تعداد قلب‌ها
            user.CurrentHearts = Math.Max(0, Math.Min(user.CurrentHearts, 10));

            // به‌روزرسانی امتیاز مسابقه
            if (!user.ContestScores.ContainsKey(contestId))
            {
                user.ContestScores[contestId] = new ContestScore
                {
                    ContestId = contestId,
                    Score = 0,
                    Hearts = user.CurrentHearts
                };
            }

            user.ContestScores[contestId].Score += scoreChange;
            user.ContestScores[contestId].Hearts = user.CurrentHearts;
            user.ContestScores[contestId].LastUpdated = DateTime.UtcNow;

            if (scoreChange > 0)
                user.ContestScores[contestId].CorrectAnswers++;
            else if (scoreChange < 0)
                user.ContestScores[contestId].WrongAnswers++;

            await SaveUserAsync(user);
            _logger.LogInformation("User score updated: {NationalCode}, Score: {ScoreChange}, Hearts: {HeartsChange}",
                nationalCode, scoreChange, heartsChange);
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            try
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = "users/"
                };

                var response = await _s3Client.ListObjectsV2Async(request);
                var users = new List<User>();

                foreach (var s3Object in response.S3Objects)
                {
                    if (s3Object.Key.EndsWith(".json"))
                    {
                        var nationalCode = Path.GetFileNameWithoutExtension(s3Object.Key);
                        var user = await GetUserAsync(nationalCode);
                        if (user != null)
                            users.Add(user);
                    }
                }

                return users;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return new List<User>();
            }
        }

        public async Task<List<User>> GetTopUsersAsync(int count)
        {
            var allUsers = await GetAllUsersAsync();
            return allUsers
                .Where(u => u.IsActive)
                .OrderByDescending(u => u.TotalScore)
                .Take(count)
                .ToList();
        }

        public async Task<bool> UserExistsAsync(string nationalCode)
        {
            var user = await GetUserAsync(nationalCode);
            return user != null;
        }

        public async Task<bool> DeleteUserAsync(string nationalCode)
        {
            try
            {
                var request = new DeleteObjectRequest
                {
                    BucketName = _bucketName,
                    Key = $"users/{nationalCode}.json"
                };

                await _s3Client.DeleteObjectAsync(request);
                _logger.LogInformation("User deleted: {NationalCode}", nationalCode);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user: {NationalCode}", nationalCode);
                return false;
            }
        }

        public async Task ResetUserHeartsAsync(string nationalCode)
        {
            var user = await GetUserAsync(nationalCode);
            if (user != null)
            {
                user.CurrentHearts = 3;
                user.LastActivity = DateTime.UtcNow;
                await SaveUserAsync(user);
            }
        }

        private async Task SaveUserAsync(User user)
        {
            try
            {
                var json = JsonSerializer.Serialize(user, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = $"users/{user.NationalCode}.json",
                    ContentBody = json,
                    ContentType = "application/json"
                };

                await _s3Client.PutObjectAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving user: {NationalCode}", user.NationalCode);
                throw;
            }
        }
    }
}
