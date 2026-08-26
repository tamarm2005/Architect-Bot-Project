# import re
# from fastapi import FastAPI, HTTPException
# from pydantic import BaseModel
# from fastapi.middleware.cors import CORSMiddleware
# from transformers import AutoModelForCausalLM, AutoTokenizer
# import torch
# import uvicorn
# import json
# import random 

# app = FastAPI(title="CAD Hebrew NLP Local Service (DictaLM)")

# app.add_middleware(
#     CORSMiddleware,
#     allow_origins=["*"],
#     allow_credentials=True,
#     allow_methods=["*"],
#     allow_headers=["*"],
# )

# chat_history = []
# MODEL_PATH = "./dictalm_model"

# print("⏳ טוען את מודל השפה מהתיקייה המקומית...")
# tokenizer = AutoTokenizer.from_pretrained(MODEL_PATH)
# device = "cuda" if torch.cuda.is_available() else "cpu"

# if device == "cuda":
#     model = AutoModelForCausalLM.from_pretrained(MODEL_PATH, torch_dtype=torch.float16).to(device)
# else:
#     model = AutoModelForCausalLM.from_pretrained(MODEL_PATH).to(device)
    
# print(f"✅ המודל מוכן לעבודה על: {device}")

# SYSTEM_PROMPT = (
#     "אתה עוזר אדריכלי מומחה, נחמד וסבלני מאוד. תפקידך היחיד והבלעדי הוא לעזור למשתמש לבנות רשימת חדרים (פרוגרמה) לדירה שלו במפלס אחד (קומה אחת) ולקבוע את הגודל שלהם במטרים מרובעים (מ\"ר).\n"
#     "חוקים קשיחים להתנהגות שלך:\n"
#     "1. דבר בשפה פשוטה, בגובה העיניים ובסבלנות, במיוחד אם המשתמש מבולבל או לא מבין במטרים.\n"
#     "2. שאל בכל פעם שאלה אחת קצרה וממוקדת בלבד! אל תציף בשאלות.\n"
#     "3. אל תוותר - תכוון את המשתמש לחלץ מידות (למשל: 'איזה גודל נרצה לסלון?', 'כמה חדרים סך הכל?').\n"
#     "4. התעלם לחלוטין מנושאים שאינם קשורים לדירות מגורים."
# )

# class ChatRequest(BaseModel):
#     message: str


# def generate_local_response(history_messages):
#     global tokenizer, model, device
#     if not history_messages:
#         return "היי! אני כאן כדי לעזור לך לבנות פרוגרמה פשוטה לדירה שלך. אילו חדרים נרצה שיהיו בה?"
        
#     dataset_files = [
#         "dataset_family_apartments.json",
#         "dataset_studio_apartments.json",
#         "dataset_accessible_housing.json",
#         "dataset_luxury_patio_flats.json"
#     ]
    
#     chosen_file = random.choice(dataset_files)
#     few_shot_text = "\n--- דוגמה להתנהלות שיחה שאותה אתה חייב לחקות: ---\n"
#     try:
#         with open(chosen_file, "r", encoding="utf-8") as f:
#             conversations_data = json.load(f)
            
#         example_chat = conversations_data[0]["chat_history"]
        
#         for turn in example_chat:
#             if turn["role"] == "user":
#                 # הגנה על קטעי ה-Few Shot בתוך תגיות ייעודיות
#                 few_shot_text += f"[INST] [USER_INPUT_START] {turn['content']} [USER_INPUT_END] [/INST] "
#             elif turn["role"] == "assistant":
#                 few_shot_text += f"{turn['content']} "
                
#         few_shot_text += "\n--- סוף דוגמה. כעת המשך את השיחה האמיתית באותו סגנון בדיוק --- \n"
#     except Exception as json_err:
#         few_shot_text = ""

#     full_prompt = f"<<SYS>>\n{SYSTEM_PROMPT}\n{few_shot_text}\n<</SYS>>\n\n"
    
#     # בניית היסטוריית השיחה האמיתית בצורה מאובטחת
#     for msg in history_messages:
#         if msg['role'] == 'user':
#             # עטיפת קלט המשתמש בתגיות הגנה כדי למנוע Prompt Injection וחטיפת המודל
#             full_prompt += f"[INST] [USER_INPUT_START]\n{msg['content']}\n[USER_INPUT_END] [/INST] "
#         elif msg['role'] == 'assistant':
#             full_prompt += f"{msg['content']} "

#     try:
#         prompt_inputs = tokenizer(full_prompt, return_tensors="pt").to(device)
#         prompt_length = prompt_inputs.input_ids.shape[1]

#         with torch.no_grad():
#             outputs = model.generate(
#                 **prompt_inputs, 
#                 max_new_tokens=150,    # מקסימום טוקנים מספק לחלוטין לתשובה ממוקדת
#                 temperature=0.1,       # טמפרטורה נמוכה מונעת הזיות והמצאת דיאלוגים
#                 do_sample=True,
#                 repetition_penalty=1.2, 
#                 pad_token_id=tokenizer.eos_token_id,
#                 # רשימת עצירה קשיחה: ברגע שהמודל מנסה לפלוט את תחילת ההודעה הבאה - הוא נעצר מיד
#                 eos_token_id=[tokenizer.eos_token_id, tokenizer.encode("[INST]")[0]] 
#             )
        
#         response_text = tokenizer.decode(outputs[0][prompt_length:], skip_special_tokens=True).strip()
#         # ניקוי שאריות של תגיות ומבנים במידה ונשארו
#         response_text = re.sub(r'\[/?INST\]|\[/?USER_INPUT_START\]|\[/?USER_INPUT_END\]|User:|Assistant:', '', response_text).strip()
#         return response_text
#     except Exception as e:
#         return "הבנתי. מה המידות שנרצה להגדיר לחלל הזה?"
    

# def fast_safe_extraction(history):
#     room_translation = {
#         "סלון": "סלון", "מטבח": "מטבח", "מרפסת": "מרפסת", 
#         "ממד": "ממ\"ד", "ממ\"ד": "ממ\"ד", "אמבטיה": "חדר רחצה", 
#         "מקלחת": "חדר רחצה", "רחצה": "חדר רחצה", "שירותים": "שירותים",
#         "עבודה": "חדר עבודה", "מחסן": "מחסן", "אוכל": "פינת אוכל"
#     }
    
#     rooms_data = []
#     current_id = 1
    
#     user_messages = [msg["content"] for msg in history if msg.get("role") == "user"]
#     if not user_messages:
#         return [], []
        
#     full_user_text = " ".join(user_messages).lower()
#     full_user_text_clean = full_user_text.replace('"', '').replace("'", "").replace("-", "")

#     # 1. חילוץ חדרים עם כמויות (לדוגמה: "3 חדרים")
#     multi_room_matches = re.findall(r'(\d+)\s+(?:חדרי|חדר|חללי|חלל)\s+([א-ת]+)', full_user_text)
#     extracted_categories = set()
    
#     for count_str, room_type in multi_room_matches:
#         try:
#             count = int(count_str)
#             base_name = room_translation.get(room_type, f"חדר {room_type}")
#             extracted_categories.add(room_type)
            
#             for i in range(1, count + 1):
#                 rooms_data.append({
#                     "id": int(current_id),
#                     "name": f"{base_name} {i}" if count > 1 else base_name,
#                     "area": 12  # שטח ברירת מחדל התחלתי
#                 })
#                 current_id += 1
#         except Exception:
#             continue

#     # 2. חילוץ חדרים בודדים מהטקסט
#     words = full_user_text_clean.split()
#     for word in words:
#         if word in room_translation and word not in extracted_categories:
#             name = room_translation[word]
#             if not any(r["name"] == name for r in rooms_data):
#                 rooms_data.append({
#                     "id": int(current_id),
#                     "name": name,
#                     "area": 12
#                 })
#                 extracted_categories.add(word)
#                 current_id += 1
                
#     # 3. חילוץ ביטויים חופשיים ("חדר משחקים")
#     custom_single_matches = re.findall(r'(?:חדר|חלל)\s+([א-ת]+)', full_user_text)
#     for match in custom_single_matches:
#         match_clean = match.strip()
#         stop_words = ["צריכה", "רוצה", "יהיה", "הזה", "שיהיה", "אחד", "נוסף", "קטן", "גדול", "שינה", "ילדים", "בערך"]
#         if match_clean not in stop_words and len(match_clean) > 2:
#             if match_clean not in extracted_categories:
#                 rooms_data.append({
#                     "id": int(current_id),
#                     "name": f"חדר {match_clean}",
#                     "area": 12
#                 })
#                 extracted_categories.add(match_clean)
#                 current_id += 1

#     # 4. מנגנון חילוץ מידות חכם
#     for msg in user_messages:
#         msg_clean = msg.lower().replace('"', '').replace("'", "").replace("-", "")
        
#         msg_clean = re.sub(r'\d+\s+(?:חדר|חדרים|חללים|חלל)', '', msg_clean)
#         msg_clean = re.sub(r'(?:חדר|חדרים|חללים|חלל)\s+\d+', '', msg_clean)
        
#         numbers = [int(n) for n in re.findall(r'\d+', msg_clean)]
            
#         for room in rooms_data:
#             simple_name = room["name"].split()[0]
#             if simple_name in msg_clean or (room["name"] in msg_clean):
                
#                 if numbers:
#                     room["area"] = int(numbers[0]) 
                
#                 elif "תגדיל" in msg_clean or "להגדיל" in msg_clean:
#                     room["area"] = int(room["area"] + 5)
#                 elif "תקטין" in msg_clean or "להקטין" in msg_clean:
#                     room["area"] = int(max(4, room["area"] - 3))

#     # 5. יצירת קשרים גרפיים (קווים)
#     lines = []
#     main_room_ids = [r["id"] for r in rooms_data if "סלון" in r["name"] or "מטבח" in r["name"]]
#     for main_id in main_room_ids:
#         for room in rooms_data:
#             if room["id"] != main_id:
#                 lines.append({"start": int(main_id), "end": int(room["id"])})

#     return rooms_data, lines


# @app.post("/api/nlp/parse")
# async def parse_command(request: ChatRequest):
#     global chat_history
#     if not request.message or request.message.strip() in ["clear", "reset", "איפוס"]:
#         chat_history = []
#         return {"success": True, "response": "השיחה אופסה. מה נרצה לתכנן?", "rooms": [], "lines": [], "is_finished": False}
    
#     try:
#         chat_history.append({"role": "user", "content": request.message})
#         response_text = generate_local_response(chat_history)
#         chat_history.append({"role": "assistant", "content": response_text})
        
#         rooms, lines = fast_safe_extraction(chat_history)
        
#         return {
#             "success": True, 
#             "response": str(response_text),
#             "is_finished": False,
#             "rooms": rooms, 
#             "lines": lines
#         }
#     except Exception as e:
#         return {
#             "success": True, 
#             "response": "הבנתי. בוא נמשיך לעדכן את רשימת החדרים והמידות.", 
#             "rooms": [], 
#             "lines": [], 
#             "is_finished": False
#         }

# if __name__ == "__main__":
#     uvicorn.run(app, host="127.0.0.1", port=5001)