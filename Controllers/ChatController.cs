using Microsoft.AspNetCore.Mvc;
using WebApplication1.Services.Interfaces;

namespace WebApplication1.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatService _chatService;
        private readonly ILogger<ChatController> _logger;

        public ChatController(IChatService chatService, ILogger<ChatController> logger)
        {
            _chatService = chatService;
            _logger = logger;
        }

        [HttpGet("history/{streamId}")]
        public async Task<IActionResult> GetChatHistory(string streamId, [FromQuery] int maxMessages = 100)
        {
            try
            {
                var history = await _chatService.GetChatHistoryAsync(streamId, maxMessages);
                return Ok(new { success = true, data = history, count = history.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat history for stream: {StreamId}", streamId);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpDelete("clear/{streamId}")]
        public async Task<IActionResult> ClearChatHistory(string streamId)
        {
            try
            {
                var result = await _chatService.ClearChatHistoryAsync(streamId);
                if (!result)
                {
                    return NotFound(new { success = false, message = "استریم یافت نشد" });
                }

                return Ok(new { success = true, message = "تاریخچه چت پاک شد" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing chat history for stream: {StreamId}", streamId);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("stats/{streamId}")]
        public async Task<IActionResult> GetChatStats(string streamId)
        {
            try
            {
                var messageCount = await _chatService.GetMessageCountAsync(streamId);
                var history = await _chatService.GetChatHistoryAsync(streamId, 50);

                var userStats = history
                    .Where(m => !m.IsSystemMessage)
                    .GroupBy(m => m.SenderNationalCode)
                    .Select(g => new
                    {
                        NationalCode = g.Key,
                        Username = g.First().SenderName,
                        MessageCount = g.Count(),
                        LastMessage = g.Max(m => m.Timestamp)
                    })
                    .OrderByDescending(u => u.MessageCount)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = new
                    {
                        TotalMessages = messageCount,
                        ActiveUsers = userStats.Count,
                        UserStatistics = userStats
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chat stats for stream: {StreamId}", streamId);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }

        [HttpGet("user-messages/{nationalCode}")]
        public async Task<IActionResult> GetUserMessages(string nationalCode, [FromQuery] string streamId = "")
        {
            try
            {
                var messages = await _chatService.GetUserMessagesAsync(nationalCode, streamId);
                return Ok(new { success = true, data = messages, count = messages.Count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user messages for: {NationalCode}", nationalCode);
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}
