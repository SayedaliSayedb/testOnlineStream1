using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using WebApplication1.Race;
using WebApplication1.Race.Hub;
using WebApplication1.Race.Services;

namespace WebApplication1.Controllers
{
    // Controllers/QuizController.cs


    [ApiController]
    [Route("api/[controller]")]
    public class QuizController : ControllerBase
    {
        private readonly IQuizDataService _dataService;
        private readonly IHubContext<QuizHub> _hubContext;

        public QuizController(IQuizDataService dataService, IHubContext<QuizHub> hubContext)
        {
            _dataService = dataService;
            _hubContext = hubContext;
        }

        [HttpGet("data")]
        public async Task<ActionResult<QuizData>> GetQuizData()
        {
            return await _dataService.GetQuizDataAsync();
        }

        [HttpPost("data")]
        public async Task<ActionResult> SaveQuizData([FromBody] QuizData quizData)
        {
            await _dataService.SaveQuizDataAsync(quizData);
            return Ok();
        }

        [HttpGet("settings")]
        public async Task<ActionResult<QuizSettings>> GetQuizSettings()
        {
            return await _dataService.GetQuizSettingsAsync();
        }

        [HttpPost("settings")]
        public async Task<ActionResult> SaveQuizSettings([FromBody] QuizSettings settings)
        {
            await _dataService.SaveQuizSettingsAsync(settings);
            return Ok();
        }

        [HttpGet("messages")]
        public async Task<ActionResult<List<Message>>> GetMessages()
        {
            return await _dataService.GetMessagesAsync();
        }

        [HttpDelete("messages")]
        public async Task<ActionResult> ClearMessages()
        {
            await _dataService.ClearMessagesAsync();
            return Ok();
        }

        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetStats()
        {
            var quizData = await _dataService.GetQuizDataAsync();
            var settings = await _dataService.GetQuizSettingsAsync();
            var state = await _dataService.GetQuizStateAsync();

            return new
            {
                TotalQuestions = quizData.Questions.Count,
                QuizTitle = settings.QuizTitle,
                HostName = settings.HostName,
                StartTime = settings.QuizStartTime,
                IsQuizActive = state.IsQuizActive,
                CurrentQuestion = state.CurrentQuestionId
            };
        }
    }
}
