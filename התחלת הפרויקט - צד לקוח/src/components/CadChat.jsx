import React, { useState, useContext, useEffect } from 'react';
import { PlotContext } from './PlotContext';
import { useNavigate } from 'react-router-dom';

const CadChat = ({ currentUser, onHistoryModeChange }) => {
    const navigate = useNavigate();
    const { programData, setProgramData, setPlotOptions } = useContext(PlotContext);

    // 🔐 ניסיון חילוץ: 1. מהפרופ | 2. מה-LocalStorage (אם קיים אצלך) | 3. ברירת מחדל 1 כדי שלא ייחסם
    const [userId, setUserId] = useState(() => {
        const propId = currentUser?.id || currentUser?.Id;
        if (propId) return propId;

        const savedUser = localStorage.getItem('currentUser');
        if (savedUser) {
            try {
                const parsed = JSON.parse(savedUser);
                return parsed?.id || parsed?.Id || null;
            } catch (e) { return null; }
        }
        return null; // ✅ בלי משתמש אמיתי - בלי שיחות, בלי נפילה למשתמש קבוע
    });

    // מעדכן את ה-userId המקומי אם ה-currentUser מהפרופס השתנה מאוחר יותר
    useEffect(() => {
        const propId = currentUser?.id || currentUser?.Id;
        if (propId) {
            setUserId(propId);
        }
    }, [currentUser]);

    // 🌟 כל קריאה לשרת מצרפת אוטומטית את הטוקן - בלי זה [Authorize] ידחה הכל
    const authFetch = (url, options = {}) => {
        const token = localStorage.getItem('authToken');
        return fetch(url, {
            ...options,
            headers: {
                ...(options.headers || {}),
                ...(token ? { 'Authorization': `Bearer ${token}` } : {})
            }
        });
    };

    const [message, setMessage] = useState('');

    const [chatHistory, setChatHistory] = useState([
        { sender: 'bot', text: 'היי! אני עוזר האדריכלות שלך בשיחה חופשית. רשמי לי מה תרצי לתכנן או לשאול היום!' }
    ]);
    const [isLoading, setIsLoading] = useState(false);

    // 🕒 סטייטים להיסטוריית השיחות
    const [pastSessions, setPastSessions] = useState([]);
    const [currentSessionId, setCurrentSessionId] = useState(null);
    const [showHistorySidebar, setShowHistorySidebar] = useState(false);

    // סטייט עבור מודאל ייעוץ תקנות
    const [showRegulationsModal, setShowRegulationsModal] = useState(false);
    const [regInput, setRegInput] = useState('');
    const [regMessages, setRegMessages] = useState([
        { role: 'bot', text: 'שלום, כאן מנוע ייעוץ התקנות והחוקים הגרפיים. במה אוכל לעזור היום?' }
    ]);
    const [isRegLoading, setIsRegLoading] = useState(false);

    // 🔄 אפקט שמנקה את הסטייט הישן ויוצר שיחה חדשה בכל פעם שהמשתמש משתנה
    useEffect(() => {
        // 1. ניקוי סטייט קיים
        setPastSessions([]);
        setCurrentSessionId(null);
        setChatHistory([
            { sender: 'bot', text: 'היי! אני עוזר האדריכלות שלך בשיחה חופשית. רשמי לי מה תרצי לתכנן או לשאול היום!' }
        ]);

        // 2. טעינת ההיסטוריה לסיידבר + יצירת שיחה חדשה נקייה
        if (userId) {
            initChatForUser(userId);
        } else {
            console.warn("⚠️ CadChat: currentUser לא הועבר לקומפוננטה או שחסר לו Id!");
        }
    }, [userId]);

    // פונקציית אתחול: טוענת היסטוריה ומייצרת שיחה חדשה אוטומטית
    const initChatForUser = async (activeUserId) => {
        try {
            const response = await authFetch(`http://localhost:5245/api/chat/user-sessions`);
            if (response.ok) {
                const data = await response.json();
                setPastSessions(data);
            }

            const chatTitle = `שיחה חדשה - ${new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
            const newSessionResponse = await authFetch(`http://localhost:5245/api/chat/new-session?title=${encodeURIComponent(chatTitle)}`, {
                method: 'POST'
            });

            if (newSessionResponse.ok) {
                const newSession = await newSessionResponse.json();
                setCurrentSessionId(newSession.id);
                setPastSessions(prev => [newSession, ...prev]);
            }
        } catch (error) {
            console.error("שגיאה באתחול השיחה החדשה למשתמש:", error);
        }
    };

    // פונקציית גישור כדי למנוע את שגיאת ה-ESLint
    const loadUserSessions = async (activeUserId) => {
        try {
            const response = await authFetch(`http://localhost:5245/api/chat/user-sessions`);
            if (response.ok) {
                const data = await response.json();
                setPastSessions(data);
            }
        } catch (error) {
            console.error("שגיאה בטעינת השיחות:", error);
        }
    };

    // פתיחת שיחה חדשה ב-DB למשתמש הנוכחי
    const handleCreateNewChat = async () => {
        if (!userId) {
            alert("לא ניתן ליצור שיחה ללא משתמש מחובר.");
            return;
        }
        try {
            const title = prompt("הזיני שם לשיחה החדשה:", `שיחה מ-${new Date().toLocaleDateString()}`);
            if (!title) return;

            const response = await authFetch(`http://localhost:5245/api/chat/new-session?title=${encodeURIComponent(title)}`, {
                method: 'POST'
            });

            if (response.ok) {
                const newSession = await response.json();
                setCurrentSessionId(newSession.id);
                setChatHistory([
                    { sender: 'bot', text: `פתחת שיחה חדשה: "${title}". רשמי לי מה תרצי לתכנן היום!` }
                ]);
                loadUserSessions(userId);
                setShowHistorySidebar(false);
                if (onHistoryModeChange) onHistoryModeChange(false);
            }
        } catch (error) {
            console.error("שגיאה ביצירת שיחה חדשה:", error);
        }
    };

    // בחירת שיחה מהרשימה
    const handleSelectSession = async (sessionId) => {
        try {
            setCurrentSessionId(sessionId);
            const response = await authFetch(`http://localhost:5245/api/chat/session-messages/${sessionId}`);
            if (response.ok) {
                const messagesFromDb = await response.json();
                if (messagesFromDb.length === 0) {
                    setChatHistory([{ sender: 'bot', text: 'השיחה הזו ריקה. רשמי הודעה כדי להתחיל!' }]);
                } else {
                    const formattedMessages = messagesFromDb.map(m => ({
                        sender: m.sender === 'user' ? 'user' : 'bot',
                        text: m.text
                    }));
                    setChatHistory(formattedMessages);
                }
            }

            // 🌟 שחזור השרטוט (רשימת החדרים) ששייך בדיוק לשיחה הזו
            const roomsResponse = await authFetch(`http://localhost:5245/api/chat/session-rooms/${sessionId}`);
            if (roomsResponse.ok) {
                const roomsFromDb = await roomsResponse.json();
                if (roomsFromDb && roomsFromDb.length > 0) {
                    setProgramData(prev => ({ ...prev, rooms: roomsFromDb }));
                }
            }

            setShowHistorySidebar(false);
            if (onHistoryModeChange) onHistoryModeChange(false); // 🌟 מציגים את השרטוט האמיתי של השיחה, לא מסתירים אותו יותר
        } catch (error) {
            console.error("שגיאה בשליפת הודעות השיחה:", error);
        }
    };

    // 🗑️ מחיקת שיחה מההיסטוריה
    const handleDeleteSession = async (e, sessionId) => {
        e.stopPropagation(); // מונע מהקליק לבחור את השיחה בטעות תוך כדי מחיקה

        if (!window.confirm("האם את בטוחה שברצונך למחוק שיחה זו לצמיתות?")) {
            return;
        }

        try {
            // פנייה לשרת ה-C# למחיקת השיחה
            const response = await authFetch(`http://localhost:5245/api/chat/session/${sessionId}`, {
                method: 'DELETE'
            });

            if (response.ok) {
                // עדכון הסטייט המקומי - מסיר את השיחה שנמחקה מהרשימה בצד
                setPastSessions(prev => prev.filter(s => s.id !== sessionId));

                // אם מחקנו את השיחה שפתוחה כרגע - ננקה את הצאט המרכזי
                if (currentSessionId === sessionId) {
                    setCurrentSessionId(null);
                    setChatHistory([
                        { sender: 'bot', text: 'השיחה נמחקה. רשמי הודעה או בחרי שיחה אחרת כדי להתחיל!' }
                    ]);
                }
            } else {
                alert("שגיאה במחיקת השיחה מהשרת.");
            }
        } catch (error) {
            console.error("שגיאה בתהליך המחיקה:", error);
            alert("שגיאה בתקשורת עם השרת בזמן המחיקה.");
        }
    };

    // שמירת ההודעות תחת ה-Session הנכון
    const saveMessageToDb = async (sessionId, sender, text) => {
        if (!sessionId) return;
        try {
            await fetch('http://localhost:5245/api/chat/add-message', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    chatSessionId: sessionId,
                    sender: sender,
                    text: text
                })
            });
        } catch (error) {
            console.error("שגיאה בשמירת ההודעה ל-DB:", error);
        }
    };

    // פונקציית שליחה משותפת המחוברת לבוט האדריכלי החדש
    const executeMessageSubmission = async (textToSend) => {
        if (!textToSend.trim()) return;
        setIsLoading(true);

        let activeSessionId = currentSessionId;
        // 1. ניהול סשן
        if (!activeSessionId && userId) {
            try {
                const response = await authFetch(`http://localhost:5245/api/chat/new-session?title=שיחה אוטומטית`, { method: 'POST' }); const newSession = await response.json();
                activeSessionId = newSession.id;
                setCurrentSessionId(activeSessionId);
                loadUserSessions(userId);
            } catch (e) { console.error("נכשלה יצירת סשן", e); }
        }

        try {
            // 2. פנייה לשרת
            const response = await authFetch('http://localhost:5245/api/chat/add-message', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ chatSessionId: activeSessionId, sender: "user", text: textToSend }),
            });
            
            if (!response.ok) throw new Error(`שרת Backend החזיר שגיאה: ${response.status}`);

            const result = await response.json();
            const botText = result.text || result.Text || "הבוט ענה ללא תוכן.";

            // 🌟 אם השרת יצר סשן חדש בעצמו, מסנכרנים את זה בצד React
            if (result.chatSessionId && !currentSessionId) {
                setCurrentSessionId(result.chatSessionId);
            }

            // 🌟 החדרים מגיעים ישירות ומובנים מהשרת - מעדכנים את סכמת הבועות בפועל
            if (result.rooms && result.rooms.length > 0) {
                const roomsWithIds = result.rooms.map((r, idx) => ({
                    id: idx + 1,
                    name: r.name || r.Name,
                    area: r.area || r.Area
                }));
                setProgramData(prev => ({ ...prev, rooms: roomsWithIds }));
                if (onHistoryModeChange) onHistoryModeChange(false);
            }

            setChatHistory(prev => [...prev, { sender: 'bot', text: botText }]);

        } catch (error) {
            console.error("Error:", error);
            setChatHistory(prev => [...prev, { sender: 'bot', text: `שגיאה בתקשורת: ${error.message}` }]);
        } finally {
            setIsLoading(false);
        }
    };

    // 🛑 חסימה מקורית וקשיחה: עדכון מבחוץ
    useEffect(() => {
        window.sendChatUpdate = (text) => {
            if (!programData?.lotArea) {
                setChatHistory(prev => [...prev, { sender: 'bot', text: '⚠️ חסר קובץ מגרש כדי לבצע פעולה זו.' }]);
                return;
            }
            setChatHistory(prev => [...prev, { sender: 'user', text: text }]);
            executeMessageSubmission(text);
        };
        return () => {
            delete window.sendChatUpdate;
        };
    }, [programData, currentSessionId]);

    // 🛑 חסימה מקורית וקשיחה: כפתור שליחה רגיל
    const handleSendMessage = (e) => {
        e.preventDefault();
        if (!message.trim()) return;

        if (!programData?.lotArea) {
            setChatHistory(prev => [
                ...prev,
                { sender: 'user', text: message },
                { sender: 'bot', text: '⚠️ לא ניתן להתחיל ביצירת פרוגרמה ותוכנית אדריכלית ללא הזנת קובץ מגרש תחילה. אנא השתמשי בכפתור "➕ העלאת מגרש" בבר העליון.' }
            ]);
            setMessage('');
            return;
        }

        setChatHistory(prev => [...prev, { sender: 'user', text: message }]);
        const currentMessage = message;
        setMessage('');
        executeMessageSubmission(currentMessage);
    };

    const handleFileUpload = async (event) => {
        const file = event.target.files[0];
        if (!file) return;

        const formData = new FormData();
        formData.append('file', file);

        try {
            const response = await fetch('http://localhost:5245/api/Cad/upload-plot', {
                method: 'POST',
                body: formData,
            });
            const data = await response.json();

            if (data.options && data.options.length > 0) {
                setPlotOptions(data.options);
                navigate("/select-plot");
            } else if (data.area) {
                setProgramData(prev => ({ ...prev, lotArea: data.area }));
                setChatHistory(prev => [...prev, { sender: 'bot', text: `✅ קובץ המגרש נקלט בהצלחה! שטח המגרש המוזהה: ${data.area} מ"ר. כעת ניתן להגדיר פרוגרמה.` }]);
            } else {
                alert("לא זוהו מגרשים בקובץ, נסי קובץ אחר.");
            }
        } catch (error) {
            console.error("שגיאה בהעלאת קובץ:", error);
            alert("שגיאה בהעלאת הקובץ.");
        }
    };

    const handleSendRegMessage = async () => {
        if (!regInput.trim()) return;

        const newMessage = { role: "user", text: regInput };
        setRegMessages(prev => [...prev, newMessage]);
        const questionToSend = regInput;
        setRegInput('');
        setIsRegLoading(true);

        try {
            const response = await fetch("http://localhost:5245/api/Cad/ask-bot", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ text: questionToSend })
            });

            if (response.ok) {
                const data = await response.json();
                setRegMessages(prev => [...prev, { role: "bot", text: data.reply }]);
            } else {
                setRegMessages(prev => [...prev, { role: "bot", text: "השרת קיבל את ההודעה אך החזיר שגיאה." }]);
            }
        } catch (error) {
            setRegMessages(prev => [...prev, { role: "bot", text: "שגיאת תקשורת עם שרת ייעוץ התקנות." }]);
        } finally {
            setIsRegLoading(false);
        }
    };

    return (
        <div style={{ display: 'flex', flexDirection: 'column', width: '100%', height: '100%', background: '#efeae2', boxSizing: 'border-box', position: 'relative' }}>

            {/* בר עליון */}
            <div style={{ padding: '10px 16px', background: '#f0f2f5', borderBottom: '1px solid #e9edef', display: 'flex', alignItems: 'center', justifyContent: 'space-between', zIndex: 10 }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <div style={{ width: '40px', height: '40px', borderRadius: '50%', background: '#00a884', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '20px', color: '#fff' }}>
                        🤖
                    </div>
                    <div>
                        <div style={{ fontWeight: 'bold', fontSize: '14px', color: '#111b21' }}>עוזר הנדסי פרוגרמה</div>
                        <div style={{ fontSize: '11px', color: programData?.lotArea ? '#16a34a' : '#ef4444', fontWeight: '600' }}>
                            {programData?.lotArea ? `Lot: ${programData.lotArea} מ"ר` : '❌ חסר קובץ מגרש'}
                        </div>
                    </div>
                </div>

                <div style={{ display: 'flex', gap: '8px', alignItems: 'center' }}>
                    <button
                        onClick={() => setShowRegulationsModal(true)}
                        style={{ padding: '6px 12px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', cursor: 'pointer', fontSize: '12px', fontWeight: '600' }}
                    >
                        ⚖️ ייעוץ תקנות
                    </button>

                    <label style={{ padding: '6px 12px', background: '#22c55e', color: '#fff', borderRadius: '6px', cursor: 'pointer', fontSize: '12px', fontWeight: '600', display: 'inline-block', margin: 0 }}>
                        ➕ העלאת מגרש
                        <input type="file" style={{ display: "none" }} onChange={handleFileUpload} />
                    </label>

                    {/* 🕒 כפתור קטן, עגול ומרחף לפתיחת ההיסטוריה */}
                    <button
                        type="button"
                        onClick={() => setShowHistorySidebar(!showHistorySidebar)}
                        style={{
                            width: '36px',
                            height: '36px',
                            background: showHistorySidebar ? '#e1f5fe' : 'transparent',
                            border: 'none',
                            borderRadius: '50%',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                            fontSize: '18px',
                            transition: 'background 0.2s',
                            color: '#54656f'
                        }}
                        title="היסטוריית שיחות"
                    >
                        🕒
                    </button>
                </div>
            </div>

            {/* 🌟 סרגל ההיסטוריה הצדדי המרחף (Overlay) - לא מכווץ את המסך ועולה ישירות מעל חלון ההודעות */}
            {showHistorySidebar && (
                <div style={{
                    position: 'absolute',
                    top: '60px',
                    right: '12px',
                    width: '220px',
                    maxHeight: 'calc(100% - 130px)',
                    background: '#202c33',
                    color: '#fff',
                    display: 'flex',
                    flexDirection: 'column',
                    borderRadius: '8px',
                    padding: '12px 8px',
                    boxSizing: 'border-box',
                    zIndex: 100,
                    boxShadow: '0 4px 12px rgba(0,0,0,0.4)'
                }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '10px', padding: '0 4px' }}>
                        <span style={{ fontSize: '11px', color: '#8696a0', fontWeight: 'bold' }}>שיחות מהעבר:</span>
                        <button
                            onClick={() => setShowHistorySidebar(false)}
                            style={{ background: 'none', border: 'none', color: '#8696a0', cursor: 'pointer', fontSize: '14px' }}
                        >
                            ✕
                        </button>
                    </div>

                    <button
                        onClick={handleCreateNewChat}
                        style={{ width: '100%', padding: '8px 5px', background: '#00a884', color: '#fff', border: 'none', borderRadius: '5px', fontWeight: 'bold', cursor: 'pointer', marginBottom: '12px', fontSize: '12px' }}
                    >
                        ➕ שיחה חדשה
                    </button>

                    <div style={{ flex: 1, overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '6px' }}>
                        {pastSessions.map((session) => (
                            <div
                                key={session.id}
                                onClick={() => handleSelectSession(session.id)}
                                style={{
                                    padding: '8px',
                                    background: currentSessionId === session.id ? '#374248' : 'transparent',
                                    borderRadius: '5px',
                                    fontSize: '12px',
                                    cursor: 'pointer',
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'space-between', // גורם לשם השיחה להיות בימין וכפתור המחיקה בשמאל
                                    borderRight: currentSessionId === session.id ? '3px solid #00a884' : 'none',
                                    color: currentSessionId === session.id ? '#fff' : '#e9edef',
                                    transition: 'background 0.2s'
                                }}
                                title={session.title}
                            >
                                {/* שם השיחה */}
                                <span style={{
                                    whiteSpace: 'nowrap',
                                    overflow: 'hidden',
                                    textOverflow: 'ellipsis',
                                    flex: 1,
                                    marginLeft: '8px'
                                }}>
                                    💬 {session.title}
                                </span>

                                {/* 🗑️ כפתור מחיקה קטן ומרחף */}
                                <button
                                    onClick={(e) => handleDeleteSession(e, session.id)}
                                    style={{
                                        background: 'none',
                                        border: 'none',
                                        color: '#ef4444',
                                        cursor: 'pointer',
                                        fontSize: '14px',
                                        padding: '2px 6px',
                                        borderRadius: '4px',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        transition: 'background 0.2s'
                                    }}
                                    onMouseEnter={(e) => e.target.style.background = 'rgba(239, 68, 68, 0.2)'}
                                    onMouseLeave={(e) => e.target.style.background = 'none'}
                                    title="מחיקת שיחה"
                                >
                                    🗑️
                                </button>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            {/* רשימת ההודעות */}
            <div style={{ flex: 1, padding: '20px', overflowY: 'auto', display: 'flex', flexDirection: 'column', gap: '10px', backgroundImage: 'url("https://user-images.githubusercontent.com/15075759/28719144-86dc0f70-73b1-11e7-911d-60d70fcded21.png")', opacity: 0.95 }}>
                {chatHistory.map((msg, index) => {
                    const isUser = msg.sender === 'user';
                    return (
                        <div key={index} style={{
                            alignSelf: isUser ? 'flex-start' : 'flex-end',
                            maxWidth: '85%',
                            background: isUser ? '#d9fdd3' : '#ffffff',
                            color: '#111b21',
                            padding: '8px 12px',
                            borderRadius: isUser ? '8px 0px 8px 8px' : '0px 8px 8px 8px',
                            boxShadow: '0 1px 0.5px rgba(11,20,26,.13)',
                            fontSize: '14px',
                            lineHeight: '1.45',
                            direction: 'rtl'
                        }}>
                            <div style={{ whiteSpace: 'pre-line' }}>{msg.text}</div>
                        </div>
                    );
                })}
                {isLoading && (
                    <div style={{ alignSelf: 'center', fontSize: '12px', color: '#54656f', background: 'rgba(255,255,255,0.85)', padding: '5px 12px', borderRadius: '7.5px' }}>
                        הבוט מחשב נתונים...
                    </div>
                )}
            </div>

            {/* אזור קלט תחתון */}
            <form onSubmit={handleSendMessage} style={{ padding: '10px 16px', background: '#f0f2f5', display: 'flex', alignItems: 'center', gap: '10px', zIndex: 10 }}>
                <input
                    type="text"
                    value={message}
                    onChange={(e) => setMessage(e.target.value)}
                    placeholder="כתבי הודעה לפרוגרמה..."
                    style={{ flex: 1, padding: '9px 12px', border: 'none', borderRadius: '8px', fontSize: '15px', outline: 'none' }}
                />
                <button type="submit" style={{ width: '40px', height: '40px', background: '#00a884', color: '#fff', border: 'none', borderRadius: '50%', cursor: 'pointer', fontSize: '16px' }}>
                    ➤
                </button>
            </form>

            {/* מודאל צף: צ'אט ייעוץ תקנות */}
            {showRegulationsModal && (
                <div style={{ position: 'absolute', top: 0, left: 0, width: '100%', height: '100%', background: 'rgba(17, 27, 33, 0.7)', zIndex: 999, display: 'flex', flexDirection: 'column' }}>
                    <div style={{ background: '#f0f2f5', padding: '12px 16px', display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid #e9edef' }}>
                        <span style={{ fontWeight: 'bold', color: '#111b21', fontSize: '14px' }}>⚖️ חלון ייעוץ תקנות וחוקים</span>
                        <button
                            onClick={() => setShowRegulationsModal(false)}
                            style={{ background: 'none', border: 'none', fontSize: '18px', cursor: 'pointer', color: '#667781' }}
                        >
                            ✕
                        </button>
                    </div>

                    <div style={{ flex: 1, padding: '16px', overflowY: 'auto', background: '#efeae2', display: 'flex', flexDirection: 'column', gap: '8px' }}>
                        {regMessages.map((msg, i) => (
                            <div key={i} style={{
                                alignSelf: msg.role === 'user' ? 'flex-start' : 'flex-end',
                                background: msg.role === 'user' ? '#e1f5fe' : '#ffffff',
                                padding: '8px 12px',
                                borderRadius: '8px',
                                maxWidth: '85%',
                                fontSize: '13.5px',
                                boxShadow: '0 1px 0.5px rgba(0,0,0,0.1)'
                            }}>
                                {msg.text}
                            </div>
                        ))}
                        {isRegLoading && <div style={{ alignSelf: 'center', fontSize: '11px', color: '#667781' }}>היועץ בודק בתקנון... ⏳</div>}
                    </div>

                    <div style={{ padding: '10px', background: '#f0f2f5', display: 'flex', gap: '8px' }}>
                        <input
                            type="text"
                            value={regInput}
                            onChange={(e) => setRegInput(e.target.value)}
                            onKeyDown={(e) => e.key === 'Enter' && handleSendRegMessage()}
                            placeholder="שאלי שאלה על קווי בניין, אחוזי בנייה וכו'..."
                            style={{ flex: 1, padding: '8px', border: 'none', borderRadius: '6px', fontSize: '14px' }}
                        />
                        <button onClick={handleSendRegMessage} style={{ padding: '8px 14px', background: '#0284c7', color: '#fff', border: 'none', borderRadius: '6px', cursor: 'pointer', fontWeight: 'bold' }}>
                            שאל
                        </button>
                    </div>
                </div>
            )}
        </div>
    );
};

export default CadChat;