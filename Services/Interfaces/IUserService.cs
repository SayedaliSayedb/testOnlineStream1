using WebApplication1.Models;

namespace WebApplication1.Services.Interfaces
{
    public interface IUserService
    {
        Task<User> GetOrCreateUserAsync(string nationalCode, string username, string profileImage);
        Task UpdateUserScoreAsync(string nationalCode, string contestId, int scoreChange, int heartsChange);
        Task<User> GetUserAsync(string nationalCode);
        Task<List<User>> GetAllUsersAsync();
        Task<bool> UserExistsAsync(string nationalCode);
        Task<bool> DeleteUserAsync(string nationalCode);
        Task<List<User>> GetTopUsersAsync(int count);
        Task ResetUserHeartsAsync(string nationalCode);
    }
}
