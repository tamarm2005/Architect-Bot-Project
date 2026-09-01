using DAL;
using Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CadProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ProjectContext _context;
        public ChatController(ProjectContext context) { _context = context; }

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
            return NoContent();
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

        // 🌟 ChatSessionId עכשיו nullable (int?) - כדי שבקשה בלי סשן לא תיפסל אוטומטית עם 400
        public class AddMessageDto
        {
            public int? ChatSessionId { get; set; }
            public string Sender { get; set; } = "";
            public string Text { get; set; } = "";
            public int? UserId { get; set; } // 🌟 גיבוי - אם אין סשן, ניצור אחד חדש בעצמנו כאן
        }

        [HttpPost("add-message")]
        public async Task<IActionResult> AddMessage([FromBody] AddMessageDto request)
        {
            try
            {
                // 🌟 אם לא הגיע מזהה סשן תקין - יוצרים סשן חדש כאן, בשרת, במקום לדחות את הבקשה
                int sessionId;
                if (request.ChatSessionId.HasValue && request.ChatSessionId.Value > 0)
                {
                    sessionId = request.ChatSessionId.Value;
                }
                else
                {
                    var newSession = new ChatSession
                    {
                        UserId = request.UserId ?? 0,
                        Title = "שיחה אוטומטית",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.ChatSessions.Add(newSession);
                    await _context.SaveChangesAsync();
                    sessionId = newSession.Id;
                }

                // שמירת הודעת המשתמש
                var userMsg = new ChatMessage { ChatSessionId = sessionId, Sender = "user", Text = request.Text, Timestamp = DateTime.UtcNow };
                _context.ChatMessages.Add(userMsg);
                await _context.SaveChangesAsync();

                // 🌟 בונים את היסטוריית השיחה המלאה - כדי שהמודל יזכור את כל מה שדובר
                var sessionMessages = await _context.ChatMessages
                    .Where(m => m.ChatSessionId == sessionId)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();

                var historyForPython = sessionMessages.Select(m => new
                {
                    role = m.Sender == "user" ? "user" : "assistant",
                    content = m.Text
                }).ToList();

                // 🌟 פנייה לשרת הפייתון המקומי (Gemini) - לא ל-Colab הישן
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromMinutes(2);

                var payload = new { history = historyForPython };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await client.PostAsync("http://127.0.0.1:5000/chat-program", content);
                var rawResponse = await response.Content.ReadAsStringAsync();

                string botReply = "מצטער, לא הצלחתי להבין. אפשר לנסות שוב?";
                var rooms = new List<object>();
                bool isFinished = false;

                try
                {
                    var root = JsonDocument.Parse(rawResponse).RootElement;

                    if (root.TryGetProperty("reply", out var replyEl))
                        botReply = replyEl.GetString() ?? botReply;

                    if (root.TryGetProperty("rooms", out var roomsEl) && roomsEl.ValueKind == JsonValueKind.Array)
                    {
                        rooms = roomsEl.EnumerateArray().Select(r => (object)new
                        {
                            name = r.TryGetProperty("name", out var n) ? n.GetString() : "חדר",
                            area = r.TryGetProperty("area", out var a) ? a.GetDouble() : 12
                        }).ToList();
                    }

                    if (root.TryGetProperty("is_finished", out var finEl))
                        isFinished = finEl.GetBoolean();
                }
                catch
                {
                    botReply = rawResponse;
                }

                var botMsg = new ChatMessage { ChatSessionId = sessionId, Sender = "assistant", Text = botReply, Timestamp = DateTime.UtcNow };
                _context.ChatMessages.Add(botMsg);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    id = botMsg.Id,
                    chatSessionId = sessionId, // 🌟 מוחזר לריאקט - כדי שהיא תעדכן את currentSessionId אם נוצר חדש כאן
                    text = botMsg.Text,
                    rooms = rooms,
                    isFinished = isFinished
                });
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Timeout Error");
                return StatusCode(500, "שרת הבינה המלאכותית לא הגיב בזמן.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("CRITICAL ERROR: " + ex.Message);
                return StatusCode(500, ex.Message);
            }
        }
    }
}