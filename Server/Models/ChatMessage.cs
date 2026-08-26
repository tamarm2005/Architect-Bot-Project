using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Models;

namespace Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public int ChatSessionId { get; set; } // קישור לשיחה אליה ההודעה שייכת
        public string Sender { get; set; } // "user" או "assistant" / "system"
        public string Text { get; set; } // תוכן ההודעה
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
