import os
import json
from google import genai
from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel
from typing import List, Optional

api_key = "AIzaSyCReUJYylhCf4tIQ7IREmOt-nPmDgW-aCY"
client = genai.Client(api_key=api_key)

print("מעלה את קובץ התקנות...")
file_path = "Construction_laws.pdf"
sample_file = client.files.upload(file=file_path)
print("הקובץ עלה בהצלחה!")

app = FastAPI()

app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:3000"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

# ============================================
# 1️⃣ כפתור "ייעוץ תקנות" - נשאר בדיוק כמו שהיה
# ============================================

class UserMessage(BaseModel):
    text: str

@app.post("/chat")
async def chat_with_bot(message: UserMessage):
    try:
        print(f"קיבלתי שאלה: {message.text}")

        response = client.models.generate_content(
            model="models/gemini-2.5-flash",
            config={
                "system_instruction": "אתה עוזר אדריכלי מומחה. ענה תמיד בעברית בלבד. הסתמך על הקובץ המצורף כדי לענות על שאלות המשתמש בצורה מקצועית וברורה."
            },
            contents=[sample_file, message.text]
        )

        if not response.text:
            return {"reply": "הבוט לא הצליח לייצר תשובה, נסה שנית."}

        print("הבוט ענה בהצלחה!")
        return {"reply": response.text}

    except Exception as e:
        print(f"שגיאה מפורטת: {str(e)}")
        return {"reply": f"שגיאה פנימית: {str(e)}"}

# ============================================
# 2️⃣ צ'אט תכנון פרוגרמה - שיחה אנושית + סכמה מובנית בכל תור
# ============================================

class ChatTurn(BaseModel):
    role: str          # "user" או "assistant"
    content: str

class ProgramChatRequest(BaseModel):
    history: List[ChatTurn]   # כל השיחה עד כה, כולל ההודעה האחרונה של המשתמש

PROGRAM_SYSTEM_INSTRUCTION = """אתה עוזר תכנון אדריכלי חם, סבלני ואנושי, שעוזר למשתמשים (שלרוב לא מבינים באדריכלות) לבנות פרוגרמה - רשימת חדרים לדירה שלהם - דרך שיחה טבעית וידידותית.

איך אתה מתנהג:
- שואל שאלה אחת ממוקדת בכל פעם, לא מציף בשאלות.
- אם המשתמש לא מבין מושג (למשל "מה זה מ\"ר") - מסביר בפשטות עם דוגמה, ואז ממשיך.
- אם המשתמש נותן גודל לחדר (למשל "חדר ילדים בגודל 10") - אתה חייב לזהות את זה מיד ולהוסיף/לעדכן אותו ברשימת rooms, לא "להתפלסף" או לענות תשובה כללית שלא קשורה.
- כשהמשתמש עמום ("תכנן לי דירה") - מציע הצעה סבירה של 3 חדרים ומסביר שאפשר לשנות.
- כשנראה שהשיחה הגיעה למקום טוב עם רשימת חדרים סבירה - שואל אם הם רוצים להמשיך או שהפרוגרמה מוכנה.

אתה חייב להחזיר תמיד אך ורק JSON תקין בפורמט הבא, בלי שום טקסט מסביב:
{
  "reply": "התגובה האנושית שלך למשתמש, בעברית, בסגנון חם ושיחתי",
  "rooms": [{"name": "סלון", "area": 20}, {"name": "מטבח", "area": 10}],
  "is_finished": false
}

כללים לגבי rooms:
- rooms הוא תמיד המצב המצטבר המלא והעדכני של כל החדרים שדובר עליהם עד כה בשיחה - לא רק החדר האחרון.
- אם משתמש לא ציין שטח לחלל - השלם שטח סביר לפי נורמות מגורים בישראל (סלון 14-30, מטבח 8-16, חדר שינה הורים 12-20, חדר ילדים 8-14, אמבטיה 3-8, שירותים 1.5-3.5, מרפסת 3-12).
- אם המשתמש מבקש חלל לא נפוץ (חדר כושר, מרתף יין וכו') - עדיין תחזיר אותו עם שם ושטח הגיוניים.
- is_finished יהיה true רק כשהמשתמש אישר בפירוש שהפרוגרמה מוכנה/סופית.

דוגמה לסגנון שיחה רצוי:
משתמש: "היי, אני רוצה לתכנן דירה חדשה. אנחנו זוג עם 3 ילדים."
אתה: {"reply": "שלום! בשמחה רבה. כדי שנוכל לבנות פרוגרמה מדויקת, כמה חדרי שינה סך הכל תרצו בדירה?", "rooms": [], "is_finished": false}
"""

@app.post("/chat-program")
async def chat_program(request: ProgramChatRequest):
    try:
        contents = []
        for turn in request.history:
            role = "user" if turn.role == "user" else "model"
            contents.append({"role": role, "parts": [{"text": turn.content}]})

        print(f"מנהל שיחת פרוגרמה, {len(contents)} תורות עד כה")

        response = client.models.generate_content(
            model="models/gemini-2.5-flash",
            config={
                "system_instruction": PROGRAM_SYSTEM_INSTRUCTION,
                "response_mime_type": "application/json"
            },
            contents=contents
        )

        if not response.text:
            return {"success": False, "error": "המודל לא החזיר תשובה"}

        parsed = json.loads(response.text)
        print(f"תגובה: {parsed.get('reply', '')[:50]}... | {len(parsed.get('rooms', []))} חדרים")

        return {
            "success": True,
            "reply": parsed.get("reply", ""),
            "rooms": parsed.get("rooms", []),
            "is_finished": parsed.get("is_finished", False)
        }

    except Exception as e:
        print(f"שגיאה בשיחת פרוגרמה: {str(e)}")
        return {"success": False, "error": str(e), "reply": "מצטער, קרתה שגיאה. אפשר לנסות שוב?"}

if __name__ == "__main__":
    import uvicorn
    print("השרת עולה בפורט 5000...")
    uvicorn.run(app, host="127.0.0.1", port=5000)

