# 🏗️ ArchitectureBot – בוט AI לתכנון שרטוטי בנייה

מערכת Full Stack המשלבת בינה מלאכותית לתכנון דירות ובתים אוטומטי, כולל יצירת שרטוטים הנדסיים (DXF), בדיקת תקינות רגולטורית, וצ'אטבוט המבוסס על חוקי בנייה בישראל.

> Full-stack AI-powered system for automated architectural floor-plan generation, DXF export, and a construction-regulation chatbot.

---

## ✨ עיקרי הפרויקט

- יצירת פריסת דירה אוטומטית (Layout Generation) על פי מגרש, סגנון ודרישות המשתמש
- מנוע אלגוריתמי לחלוקת חללים (BSP Splitting, Room Solver, Greedy Layout Engine)
- מיקום ריהוט אוטומטי (Furniture Placement Algorithm)
- יצירת גגות, חתכים (Sections) וחזיתות (Elevations) אוטומטית
- ייצוא שרטוטים לקובצי DXF ו-ZIP מלא של הפרויקט
- צ'אטבוט AI (מבוסס Google Gemini) לייעוץ בענייני תקנות בנייה, עם התבססות על קובץ תקנות רשמי
- ניהול משתמשים, פרויקטים והיסטוריית שיחות

---

## 🏛️ ארכיטקטורה

המערכת בנויה משלושה חלקים נפרדים שמתקשרים ביניהם:

```
┌─────────────────┐      HTTP/REST       ┌──────────────────────┐
│   Frontend       │ ───────────────────▶ │   Backend API (.NET)  │
│   React (src/)    │ ◀─────────────────── │   ASP.NET Core        │
└─────────────────┘                       └──────────┬───────────┘
                                                       │
                                            ┌──────────▼───────────┐
                                            │   SQL Server (EF Core)│
                                            └───────────────────────┘

┌─────────────────────────┐     HTTP      ┌──────────────────────┐
│  AI / NLP Service         │ ◀──────────── │  Backend API (.NET)   │
│  Python FastAPI            │ ────────────▶ │                        │
│  (ArchitectureBot/)        │               └──────────────────────┘
└─────────────────────────┘
```

הבקאנד עצמו בנוי במבנה שכבות (Layered Architecture):
- **API** – Controllers, נקודות קצה, Swagger
- **BL** (Business Logic) – כל האלגוריתמים ההנדסיים (חישובי גיאומטריה, חלוקת חללים, גגות, מידות וכו')
- **DAL** (Data Access Layer) – תקשורת מול מסד הנתונים דרך Entity Framework Core
- **Models** – ישויות המערכת (דירה, חדר, קיר, פתח, ריהוט, משתמש וכו')

---

## 🧰 טכנולוגיות

| שכבה | טכנולוגיות |
|---|---|
| **Frontend** | React, React Router, JSX, CSS |
| **Backend** | C# / ASP.NET Core, Entity Framework Core, LINQ |
| **AI / NLP Service** | Python, FastAPI, Google Gemini API |
| **מסד נתונים** | SQL Server |
| **ייצוא שרטוטים** | ezdxf, Flask (שירות עזר ליצירת DXF) |

---

## 📁 מבנה הפרויקט

```
├── src/                      # קוד ה-Frontend (React)
│   └── components/           # CadWorkspace, CadChat, SelectPlot, Home ועוד
│
├── Server/                   # קוד ה-Backend (ASP.NET Core, פתרון בשכבות)
│   ├── API/                  # Controllers, Program.cs, Swagger
│   ├── BL/Algorithms/        # כל אלגוריתמי התכנון וההנדסה
│   ├── DAL/                  # Entity Framework, Migrations
│   └── Models/                # ישויות המערכת
│
└── ArchitectureBot/           # שירות ה-AI (Python)
    ├── main.py                # שרת FastAPI - צ'אטבוט תקנות בנייה (Gemini API)
    ├── create_dxf.py          # שירות Flask ליצירת קובצי DXF
    └── Construction_laws.pdf  # מאגר התקנות שעליו מתבסס הבוט
```

---

## ⚙️ התקנה והרצה

### דרישות מקדימות
- .NET 8 SDK
- Node.js + npm
- Python 3.10+
- SQL Server (LocalDB / SQLEXPRESS)

### 1. הרצת ה-Backend (ASP.NET Core)
```bash
cd Server/Server/API
dotnet restore
dotnet ef database update      # יצירת מסד הנתונים לפי ה-Migrations
dotnet run
```
השרת יעלה בכתובת: `http://localhost:5245` (Swagger: `/swagger`)

### 2. הרצת שירות ה-AI (Python)
```bash
cd ArchitectureBot/ArchitectureBot
python -m venv .venv
.venv\Scripts\activate          # ב-Windows | source .venv/bin/activate ב-Mac/Linux
pip install fastapi uvicorn google-genai python-dotenv ezdxf flask flask-cors
uvicorn main:app --reload
```

### 3. הרצת ה-Frontend (React)
```bash
cd src
npm install
npm start
```
האפליקציה תיפתח בכתובת: `http://localhost:3000`

---

## 🔌 נקודות קצה עיקריות (API)

| Method | Endpoint | תיאור |
|---|---|---|
| POST | `/api/cad/generate-layout` | יצירת פריסת דירה אוטומטית |
| POST | `/api/cad/generate-roof` | יצירת גג אוטומטי |
| POST | `/api/cad/generate-elevations` | יצירת חזיתות |
| POST | `/api/cad/generate-sections` | יצירת חתכים |
| POST | `/api/cad/arrange-room/{roomId}` | סידור ריהוט אוטומטי בחדר |
| POST | `/api/cad/export-full-layout-dxf` | ייצוא שרטוט מלא כ-DXF |
| POST | `/api/cad/export-project-zip` | ייצוא כל הפרויקט כ-ZIP |
| POST | `/api/cad/ask-bot` | שאלה לצ'אטבוט תקנות בנייה |
| POST | `/api/chat/new-session` | פתיחת שיחה חדשה |
| POST | `/api/user/login` | התחברות משתמש |

*(הרשימה המלאה זמינה ב-Swagger לאחר הרצת השרת)*

---

## 🚀 פיתוחים עתידיים

- זיהוי ריהוט וחללים באמצעות למידת מכונה
- ייצוא ישיר לפורמט DWG (AutoCAD)
- שילוב ניתוח תרמי לאיתור מוקדי איבוד אנרגיה

---

## 👩‍💻 פותח על ידי

תמר מאזוז