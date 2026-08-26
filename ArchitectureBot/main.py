import os
from google import genai
from fastapi import FastAPI
from pydantic import BaseModel

api_key = "AIzaSyCReUJYylhCf4tIQ7IREmOt-nPmDgW-aCY"
client = genai.Client(api_key=api_key)

print("מעלה את קובץ התקנות...")
file_path = "Construction_laws.pdf"
sample_file = client.files.upload(file=file_path)
print("הקובץ עלה בהצלחה!")

app = FastAPI()

from fastapi.middleware.cors import CORSMiddleware

app.add_middleware(
    CORSMiddleware,
    allow_origins=["http://localhost:3000"], 
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

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
            
        print(f"הבוט ענה בהצלחה!") 
        return {"reply": response.text}
        
    except Exception as e:
        print(f"שגיאה מפורטת: {str(e)}")
        return {"reply": f"שגיאה פנימית: {str(e)}"}
    
import io
import ezdxf
from fastapi import UploadFile, File
from fastapi.responses import StreamingResponse

@app.post("/process-dxf")
async def process_dxf(file: UploadFile = File(...)):
    try:
        content = await file.read()
        doc = ezdxf.readbytes(content)
        msp = doc.modelspace()

        for line in msp.query('LINE'):
            start = line.dxf.start
            end = line.dxf.end
            msp.add_linear_dim(base=(start.x, start.y + 15), p1=start, p2=end).render()

        out_buffer = io.BytesIO()
        doc.write(out_buffer)
        out_buffer.seek(0)

        return StreamingResponse(
            out_buffer,
            media_type="application/dxf",
            headers={"Content-Disposition": f"attachment; filename=new_plan.dxf"}
        )
    except Exception as e:
        return {"error": str(e)}

# ============================================
# 🌟 Endpoint חדש: חילוץ רשימת חללים מטקסט חופשי
# לא נוגע בקובץ החוקים/בכפתור הייעוץ הקיים
# ============================================

class RoomExtractionRequest(BaseModel):
    text: str

@app.post("/parse-rooms")
async def parse_rooms(request: RoomExtractionRequest):
    try:
        print(f"מנתח בקשת חללים: {request.text}")

        response = client.models.generate_content(
            model="models/gemini-2.5-flash",
            config={
                "system_instruction": """אתה עוזר תכנון אדריכלי. המשתמש יתאר בשפה חופשית (עברית) אילו חללים הוא רוצה בדירה.
תפקידך: לזהות רשימת חללים ולהחזיר אך ורק JSON תקין בפורמט הבא, בלי שום טקסט נוסף מסביב, בלי ```json ובלי הסברים:
{"rooms": [{"name": "סלון", "area": 20}, {"name": "מטבח", "area": 10}]}

כללים:
- אם המשתמש לא ציין שטח לחלל מסוים - השלימי שטח סביר בעצמך לפי נורמות מגורים בישראל (סלון 14-30, מטבח 8-14, חדר שינה הורים 12-16, חדר ילדים 8-12, אמבטיה 4-7, שירותים 1.5-3, מרפסת 4-10).
- אם המשתמש מבקש חלל לא נפוץ (למשל 'חדר כושר' או 'מרתף יין') - עדיין החזירי אותו עם שם ושטח הגיוניים, אל תסרבי ואל תמציאי תשובה מחוץ ל-JSON.
- אם הבקשה עמומה לגמרי (למשל 'תכנן לי דירה') - החזירי הצעה סבירה לדירת 3 חדרים סטנדרטית.
- תמיד תחזירי rooms כמערך, גם אם יש חלל אחד בלבד.""",
                "response_mime_type": "application/json"
            },
            contents=[request.text]
        )

        if not response.text:
            return {"success": False, "error": "המודל לא החזיר תשובה"}

        import json
        parsed = json.loads(response.text)
        print(f"חולצו {len(parsed.get('rooms', []))} חללים בהצלחה")
        return {"success": True, "rooms": parsed.get("rooms", [])}

    except Exception as e:
        print(f"שגיאה בחילוץ חללים: {str(e)}")
        return {"success": False, "error": str(e)}

if __name__ == "__main__":
    import uvicorn
    print("השרת עולה בפורט 5000...")
    uvicorn.run(app, host="127.0.0.1", port=5000)