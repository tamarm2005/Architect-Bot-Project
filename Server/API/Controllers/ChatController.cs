using DAL;
using Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Nodes;

namespace CadProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ProjectContext _context;
        public ChatController(ProjectContext context) { _context = context; }

        // יוצר סישן חדש
        [HttpPost("new-session")]
        public async Task<IActionResult> CreateSession([FromQuery] int userId, [FromQuery] string title)
        {
            var session = new ChatSession { UserId = userId, Title = title, CreatedAt = DateTime.UtcNow };
            _context.ChatSessions.Add(session);
            await _context.SaveChangesAsync();
            return Ok(session);
        }

        [HttpDelete("session/{sessionId}")]
        public async Task<IActionResult> DeleteSession(int sessionId)
        {
            var session = await _context.ChatSessions.FindAsync(sessionId);
            if (session == null) return NotFound();

            _context.ChatSessions.Remove(session);
            await _context.SaveChangesAsync();
            return NoContent(); // מוחק ומחזיר אישור שהסתיים בהצלחה
        }

        [HttpGet("user-sessions/{userId}")]
        public async Task<IActionResult> GetUserSessions(int userId)
        {
            var sessions = await _context.ChatSessions.Where(s => s.UserId == userId).OrderByDescending(s => s.CreatedAt).ToListAsync();
            return Ok(sessions);
        }

        [HttpGet("session-messages/{sessionId}")]
        public async Task<IActionResult> GetSessionMessages(int sessionId)
        {
            var messages = await _context.ChatMessages.Where(m => m.ChatSessionId == sessionId).OrderBy(m => m.Timestamp).ToListAsync();
            return Ok(messages);
        }

        [HttpPost("add-message")]
        public async Task<IActionResult> AddMessage([FromBody] ChatMessage request)
        {
            try
            {
                // שמירת הודעת המשתמש
                var userMsg = new ChatMessage { ChatSessionId = request.ChatSessionId, Sender = "user", Text = request.Text, Timestamp = DateTime.UtcNow };
                _context.ChatMessages.Add(userMsg);
                await _context.SaveChangesAsync();

                // פנייה לקולאב
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(3);
                var payload = new { model = "architecture-bot", prompt = request.Text, stream = false };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await client.PostAsync("https://varying-claw-dimly.ngrok-free.dev/api/generate", content);

                // התיקון הקריטי: כאן הגדרנו את המשתנה rawResponse
                var rawResponse = await response.Content.ReadAsStringAsync();

                // חילוץ המידע
                string botReply = "";
                try
                {
                    var root = JsonDocument.Parse(rawResponse).RootElement;

                    if (root.TryGetProperty("response", out var res) && !string.IsNullOrEmpty(res.GetString()))
                    {
                        botReply = res.GetString();
                    }
                    else if (root.TryGetProperty("thinking", out var think) && !string.IsNullOrEmpty(think.GetString()))
                    {
                        botReply = think.GetString();
                    }
                    else
                    {
                        botReply = rawResponse; // גיבוי: נציג את כל ה-JSON אם לא מצאנו שדות
                    }
                }
                catch
                {
                    botReply = rawResponse; // אם ה-JSON דפוק, נציג את הטקסט הגולמי
                }

                // שמירת הודעת הבוט
                var botMsg = new ChatMessage { ChatSessionId = request.ChatSessionId, Sender = "assistant", Text = botReply, Timestamp = DateTime.UtcNow };
                _context.ChatMessages.Add(botMsg);
                await _context.SaveChangesAsync();

                return Ok(botMsg);
            }
            catch (TaskCanceledException)
            {
                // זה מה שיקפוץ אם הבוט באמת עבר את ה-5 דקות (במקום השגיאה 500
                Console.WriteLine("Timeout Error");
                return StatusCode(500, "השרת בקולאב חשב יותר מדי זמן (מעל 5 דקות). ודאי שמוגדר לך GPU בקולאב.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("CRITICAL ERROR: " + ex.Message);
                return StatusCode(500, ex.Message);
            }
        }
    }
}