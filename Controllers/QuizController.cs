using Microsoft.AspNetCore.Mvc;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuizController : ControllerBase
    {
        private readonly IQuizService _quizService;
        private readonly ILogger<QuizController> _logger;

        public QuizController(IQuizService quizService, ILogger<QuizController> logger)
        {
            _quizService = quizService;
            _logger = logger;
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateQuestion([FromBody] CreateQuestionRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Question) || request.Options == null || request.Options.Count != 4)
                {
                    return BadRequest(new { success = false, message = "سوال و 4 گزینه الزامی است" });
                }

                if (request.CorrectAnswerIndex < 0 || request.CorrectAnswerIndex > 3)
                {
                    return BadRequest(new { success = false, message = "گزینه صحیح باید بین 0 تا 3 باشد" });
                }

                var question = await _quizService.CreateQuestionAsync(
                    request.Question,
                    request.Options,
                    request.CorrectAnswerIndex,
                    request.TimeLimit,
                    request.ContestId
                );

                _logger.LogInformation("Question created: {QuestionId}", question.Id);
                return Ok(new { success = true, data = question });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating question");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost("start")]
        public async Task<IActionResult> StartQuiz([FromBody] StartQuizRequest request)
        {
            try
            {
                var quizInfo = await _quizService.StartQuizAsync(request.Question, request.StreamId);

                _logger.LogInformation("Quiz started: {QuizId} for stream: {StreamId}", quizInfo.Id, request.StreamId);
                return Ok(new { success = true, data = quizInfo });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting quiz");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpPost("submit-answer")]
        public async Task<IActionResult> SubmitAnswer([FromBody] SubmitAnswerRequest request)
        {
            try
            {
                var result = await _quizService.SubmitAnswerAsync(
                    request.QuizId,
                    request.ConnectionId,
                    request.NationalCode,
                    request.AnswerIndex
                );

                if (!result)
                {
                    return BadRequest(new { success = false, message = "امکان ثبت پاسخ وجود ندارد" });
                }

                return Ok(new { success = true, message = "پاسخ با موفقیت ثبت شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting answer");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("active")]
        public async Task<IActionResult> GetActiveQuizzes()
        {
            try
            {
                var quizzes = await _quizService.GetActiveQuizzesAsync();
                return Ok(new { success = true, data = quizzes });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active quizzes");
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("{quizId}")]
        public async Task<IActionResult> GetQuiz(string quizId)
        {
            try
            {
                var quiz = await _quizService.GetQuizAsync(quizId);
                if (quiz == null)
                {
                    return NotFound(new { success = false, message = "مسابقه یافت نشد" });
                }

                return Ok(new { success = true, data = quiz });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting quiz: {QuizId}", quizId);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }

    public class CreateQuestionRequest
    {
        public string Question { get; set; } = string.Empty;
        public List<string> Options { get; set; } = new();
        public int CorrectAnswerIndex { get; set; }
        public int TimeLimit { get; set; } = 30;
        public string ContestId { get; set; } = "default";
    }

    public class StartQuizRequest
    {
        public QuizQuestion Question { get; set; } = new();
        public string StreamId { get; set; } = string.Empty;
    }

    public class SubmitAnswerRequest
    {
        public string QuizId { get; set; } = string.Empty;
        public string ConnectionId { get; set; } = string.Empty;
        public string NationalCode { get; set; } = string.Empty;
        public int AnswerIndex { get; set; }
    }
}
