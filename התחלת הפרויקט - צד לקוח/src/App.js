import React, { useState } from "react";
import { Routes, Route, useNavigate } from "react-router-dom";
import RegistrationSite from "./components/RegistrationSite";
import LoginSite from "./components/LoginSite";
import SelectPlot from "./components/SelectPlot";
import CadWorkspace from "./components/CadWorkspace";
import D from "./components/D";
import "./components/Style.css";
import Home from "./components/Home";

function App() {
  let navigate = useNavigate();

  // סטייט ניהול משתמש מחובר
  const [currentUser, setCurrentUser] = useState(null);

  React.useEffect(() => {
    const saved = localStorage.getItem('currentUser');
    if (saved) {
      try { setCurrentUser(JSON.parse(saved)); } catch (e) { }
    }
  }, []);

  // 🌟 כניסת משתמש - עכשיו שומר גם את ה-Token (JWT) שחוזר מהשרת
  const enterUser = (user, token) => {
    setCurrentUser(user);
    localStorage.setItem('currentUser', JSON.stringify(user));
    if (token) localStorage.setItem('authToken', token);
    navigate("/plan-program");
  };

  const handleLogout = () => {
    setCurrentUser(null);
    localStorage.removeItem('currentUser');
    localStorage.removeItem('authToken');
    navigate("/");
  };

  return (
    <>
      {/* 🟢 סרגל עליון מעוצב מחדש לפי הבקשה שלך */}
      <div id="bar" style={{
        display: "flex",
        justifyContent: "space-between",
        alignItems: "center",
        padding: "10px 20px",
        background: "#075e54",
        height: "50px",
        boxSizing: "border-box",
        direction: "rtl"
      }}>

        {/* ימין: שלום למשתמש המחובר */}
        <div style={{ width: "33%", textAlign: "right" }}>
          {currentUser ? (
            <span style={{ color: "#25d366", fontWeight: "600", fontSize: "14px" }}>
              שלום, {currentUser.userName} 👋
            </span>
          ) : (
            <span style={{ color: "rgba(255,255,255,0.5)", fontSize: "13px" }}>אורח במערכת</span>
          )}
        </div>

        {/* מרכז: כותרת האפליקציה באמצע */}
        <div style={{ width: "34%", textAlign: "center" }}>
          <span style={{ color: "white", fontWeight: "bold", fontSize: "18px", letterSpacing: "0.5px" }}>
            🤖 צ'אט אדריכלי חכם
          </span>
        </div>

        {/* שמאל: כפתור התנתקות יפה בצד שמאל (רק אם מחובר) */}
        <div style={{ width: "33%", textAlign: "left" }}>
          {currentUser && (
            <button
              onClick={handleLogout}
              style={{
                background: "rgba(255, 255, 255, 0.15)",
                color: "#ffffff",
                border: "1px solid rgba(255, 255, 255, 0.3)",
                borderRadius: "6px",
                padding: "4px 12px",
                cursor: "pointer",
                fontSize: "12px",
                fontWeight: "600",
                transition: "all 0.2s"
              }}
              onMouseEnter={(e) => e.currentTarget.style.background = "rgba(255, 255, 255, 0.25)"}
              onMouseLeave={(e) => e.currentTarget.style.background = "rgba(255, 255, 255, 0.15)"}
            >
              🚪 התנתק
            </button>
          )}
        </div>
      </div>

      <div className="content-area">
        <Routes>
          <Route path="/" element={< Home />} />
          <Route path="/RegistrationSite" element={
            <RegistrationSite enterUser={enterUser} />
          } />
          <Route path="/LoginSite" element={
            <LoginSite enterUser={enterUser} />
          } />
          <Route path="/select-plot" element={<SelectPlot />} />
          <Route path="/plan-program" element={
            currentUser
              ? <CadWorkspace currentUser={currentUser} onLogout={handleLogout} />
              : <LoginSite enterUser={enterUser} />
          } />
          <Route path="/d" element={<D />} />
        </Routes>
      </div>
    </>
  );
}

export default App;