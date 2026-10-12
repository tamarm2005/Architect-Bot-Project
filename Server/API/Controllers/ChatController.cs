using DAL;
using Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CadProject.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // 🌟 כל הפעולות כאן דורשות טוקן תקין בכותרת Authorization
    public class ChatController : ControllerBase
    {
        private readonly ProjectContext _context;
        public ChatController(ProjectContext context) { _context = context; }

        // 🌟 שולף את מזהה המשתמש מתוך הטוקן עצמו - לא סומכים על מה שהלקוח שולח
        private int GetUserIdFromToken()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        [HttpPost("new-session")]
        public async Task<IActionResult> CreateSession([FromQuery] string title)
        {
            int userId = GetUserIdFromToken();
            var session = new ChatSession { UserId = userId, Title = title, CreatedAt = DateTime.UtcNow };
            _context.ChatSessions.Add(session);
            await _context.SaveChangesAsync();
            return Ok(session);
        }

        [HttpDelete("session/{sessionId}")]
        public async Task<IActionResult> DeleteSession(int sessionId)
        {
            int userId = GetUserIdFromToken();
            var session = await _context.ChatSessions.FindAsync(sessionId);
            if (session == null) return NotFound();
            if (session.UserId != userId) return Forbid(); // 🌟 אי אפשר למחוק שיחה שלא שייכת לך

            _context.ChatSessions.Remove(session);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpGet("user-sessions")]
        public async Task<IActionResult> GetUserSessions()
        {
            int userId = GetUserIdFromToken();
            var sessions = await _context.ChatSessions.Where(s => s.UserId == userId).OrderByDescending(s => s.CreatedAt).ToListAsync();
            return Ok(sessions);
        }

        [HttpGet("session-messages/{sessionId}")]
        public async Task<IActionResult> GetSessionMessages(int sessionId)
        {
            int userId = GetUserIdFromToken();
            var session = await _context.ChatSessions.FindAsync(sessionId);
            if (session == null) return NotFound();
            if (session.UserId != userId) return Forbid(); // 🌟 אי אפשר לקרוא שיחה של מישהו אחר

            var messages = await _context.ChatMessages.Where(m => m.ChatSessionId == sessionId).OrderBy(m => m.Timestamp).ToListAsync();
            return Ok(messages);
        }

        [HttpGet("session-rooms/{sessionId}")]
        public async Task<IActionResult> GetSessionRooms(int sessionId)
        {
            int userId = GetUserIdFromToken();
            var session = await _context.ChatSessions.FindAsync(sessionId);
            if (session == null) return NotFound();
            if (session.UserId != userId) return Forbid();

            if (string.IsNullOrEmpty(session.RoomsSnapshot)) return Ok(new List<object>());
            var rooms = JsonSerializer.Deserialize<List<object>>(session.RoomsSnapshot);
            return Ok(rooms);
        }

        public class AddMessageDto
        {
            public int? ChatSessionId { get; set; }
            public string Sender { get; set; } = "";
            public string Text { get; set; } = "";
        }

        [HttpPost("add-message")]
        public async Task<IActionResult> AddMessage([FromBody] AddMessageDto request)
        {
            try
            {
                int userId = GetUserIdFromToken();
                int sessionId;

                if (request.ChatSessionId.HasValue && request.ChatSessionId.Value > 0)
                {
                    var existingSession = await _context.ChatSessions.FindAsync(request.ChatSessionId.Value);
                    if (existingSession == null || existingSession.UserId != userId)
                        return Forbid(); // 🌟 אי אפשר לכתוב לתוך שיחה של מישהו אחר

                    sessionId = request.ChatSessionId.Value;
                }
                else
                {
                    var newSession = new ChatSession { UserId = userId, Title = "שיחה אוטומטית", CreatedAt = DateTime.UtcNow };
                    _context.ChatSessions.Add(newSession);
                    await _context.SaveChangesAsync();
                    sessionId = newSession.Id;
                }

                var userMsg = new ChatMessage { ChatSessionId = sessionId, Sender = "user", Text = request.Text, Timestamp = DateTime.UtcNow };
                _context.ChatMessages.Add(userMsg);
                await _context.SaveChangesAsync();

                var sessionMessages = await _context.ChatMessages
                    .Where(m => m.ChatSessionId == sessionId)
                    .OrderBy(m => m.Timestamp)
                    .ToListAsync();

                var historyForPython = sessionMessages.Select(m => new { role = m.Sender == "user" ? "user" : "assistant", content = m.Text }).ToList();

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
                    if (root.TryGetProperty("reply", out var replyEl)) botReply = replyEl.GetString() ?? botReply;
                    if (root.TryGetProperty("rooms", out var roomsEl) && roomsEl.ValueKind == JsonValueKind.Array)
                        rooms = roomsEl.EnumerateArray().Select(r => (object)new
                        {
                            name = r.TryGetProperty("name", out var n) ? n.GetString() : "חדר",
                            area = r.TryGetProperty("area", out var a) ? a.GetDouble() : 12
                        }).ToList();
                    if (root.TryGetProperty("is_finished", out var finEl)) isFinished = finEl.GetBoolean();
                }
                catch { botReply = rawResponse; }

                var botMsg = new ChatMessage { ChatSessionId = sessionId, Sender = "assistant", Text = botReply, Timestamp = DateTime.UtcNow };
                _context.ChatMessages.Add(botMsg);

                // 🌟 שומרים "תמונת מצב" עדכנית של החדרים על השיחה עצמה - כדי שנוכל לשחזר אותה מאוחר יותר
                if (rooms.Count > 0)
                {
                    var sessionToUpdate = await _context.ChatSessions.FindAsync(sessionId);
                    if (sessionToUpdate != null)
                        sessionToUpdate.RoomsSnapshot = JsonSerializer.Serialize(rooms);
                }

                await _context.SaveChangesAsync();

                return Ok(new { id = botMsg.Id, chatSessionId = sessionId, text = botMsg.Text, rooms = rooms, isFinished = isFinished });
            }
            catch (TaskCanceledException) { return StatusCode(500, "שרת הבינה המלאכותית לא הגיב בזמן."); }
            catch (Exception ex) { Console.WriteLine("CRITICAL ERROR: " + ex.Message); return StatusCode(500, ex.Message); }
        }
    }
}