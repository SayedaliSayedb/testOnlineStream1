using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services.Interfaces;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(IUserService userService, ILogger<AuthController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.NationalCode))
                {
                    return BadRequest(new { success = false, message = "کد ملی الزامی است" });
                }

                if (request.NationalCode.Length != 10)
                {
                    return BadRequest(new { success = false, message = "کد ملی باید 10 رقمی باشد" });
                }

                var user = await _userService.GetOrCreateUserAsync(
                    request.NationalCode,
                    request.Username ?? $"کاربر_{request.NationalCode}",
                    request.ProfileImage ?? "");

                return Ok(new
                {
                    success = true,
                    message = "کاربر با موفقیت ثبت‌نام شد",
                    user = new
                    {
                        user.NationalCode,
                        user.Username,
                        user.ProfileImage,
                        user.TotalScore,
                        user.CurrentHearts,
                        user.CreatedAt
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in user registration for national code: {NationalCode}", request.NationalCode);
                return StatusCode(500, new { success = false, message = "خطا در ثبت‌نام کاربر" });
            }
        }

        [HttpGet("user/{nationalCode}")]
        public async Task<IActionResult> GetUser(string nationalCode)
        {
            try
            {
                var user = await _userService.GetUserAsync(nationalCode);
                if (user == null)
                {
                    return NotFound(new { success = false, message = "کاربر یافت نشد" });
                }

                return Ok(new
                {
                    success = true,
                    user = new
                    {
                        user.NationalCode,
                        user.Username,
                        user.ProfileImage,
                        user.TotalScore,
                        user.CurrentHearts,
                        user.ContestScores,
                        user.CreatedAt,
                        user.LastActivity
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user: {NationalCode}", nationalCode);
                return StatusCode(500, new { success = false, message = "خطا در دریافت اطلاعات کاربر" });
            }
        }

        [HttpPost("reset-hearts")]
        public async Task<IActionResult> ResetHearts([FromBody] ResetHeartsRequest request)
        {
            try
            {
                await _userService.ResetUserHeartsAsync(request.NationalCode);
                return Ok(new { success = true, message = "قلب‌های کاربر بازنشانی شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting hearts for user: {NationalCode}", request.NationalCode);
                return StatusCode(500, new { success = false, message = "خطا در بازنشانی قلب‌ها" });
            }
        }

        [HttpGet("leaderboard")]
        public async Task<IActionResult> GetLeaderboard([FromQuery] int top = 10)
        {
            try
            {
                var topUsers = await _userService.GetTopUsersAsync(top);
                return Ok(new { success = true, users = topUsers });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting leaderboard");
                return StatusCode(500, new { success = false, message = "خطا در دریافت لیست برترین‌ها" });
            }
        }
    }

    public class RegisterRequest
    {
        public string NationalCode { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string ProfileImage { get; set; } = string.Empty;
    }

    public class ResetHeartsRequest
    {
        public string NationalCode { get; set; } = string.Empty;
    }
}
