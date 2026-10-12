using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Models
{
    public class ChatSession
    {
        public int Id { get; set; }
        public int UserId { get; set; } // קישור למשתמש
        public string Title { get; set; } // כותרת השיחה (למשל: "שיחה מ-24/06/2026")
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // קשר של אחד לרבים - לכל שיחה יש הרבה הודעות
        public List<ChatMessage> Messages { get; set; } = [];

        public string? RoomsSnapshot { get; set; } //  "תמונת מצב" של רשימת החדרים ששייכת לשיחה הזו
    }
}
