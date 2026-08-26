// הרשמה 

import React, { useState } from "react";
import { useNavigate } from "react-router-dom"; // 🌟 הוספנו ניווט

export default function RegistrationSite({ userList, setUserList, enterUser }) {
  const [userName, setUserName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  let navigate = useNavigate(); // 🌟 הגדרת הניווט

  return (
    <div className="auth-wrapper">
      <div className="auth-card">
        <h1>מסך הרשמה</h1>
        <form className="auth-form" onSubmit={(e) => e.preventDefault()}>
          <input 
            type="text" 
            placeholder="שם משתמש" 
            className="auth-input"
            onChange={(e) => setUserName(e.target.value)} 
          />
          <input 
            type="email" 
            placeholder="כתובת אימייל" 
            className="auth-input"
            onChange={(e) => setEmail(e.target.value)} 
          />
          <input 
            type="password" 
            placeholder="סיסמה חדשה" 
            className="auth-input"
            onChange={(e) => setPassword(e.target.value)} 
          />
          <button
            type="button"
            className="btn-primary"
            style={{ marginTop: "10px" }}
            onClick={() => {
              if (userList.find(u => u.email === email)) {
                alert("משתמש כבר קיים!");
              } else {
                const newUser = { userName, email, password };
                setUserList([...userList, newUser]);
                alert("נרשמת בהצלחה!");
                enterUser(newUser);
              }
            }}
          >
            הרשם והכנס
          </button>
        </form>

        {/* 🌟 לחצן חזרה/מעבר להתחברות מעוצב ועדין */}
        <div style={{ marginTop: "20px", fontSize: "14px", color: "#666", textAlign: "center" }}>
          <span>כבר נרשמת בעבר? </span>
          <button 
            type="button" 
            onClick={() => navigate("/LoginSite")}
            style={{
              background: "none",
              border: "none",
              color: "#00a884",
              fontWeight: "bold",
              textDecoration: "underline",
              cursor: "pointer",
              padding: "0",
              fontSize: "14px"
            }}
          >
            להתחברות למערכת
          </button>
        </div>

      </div>
    </div>
  );
}