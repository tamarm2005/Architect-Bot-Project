// התחברות 

import React, { useState } from "react"; 
import { useNavigate } from "react-router-dom"; // 🌟 הוספנו ניווט

export default function LoginSite({ userList, enterUser }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  let navigate = useNavigate(); // 🌟 הגדרת הניווט
  
  return (
    <div className="auth-wrapper">
      <div className="auth-card">
        <h1>מסך התחברות</h1>
        <form className="auth-form" onSubmit={(e) => e.preventDefault()}>
          <input
            type="email"
            placeholder="כתובת אימייל"
            className="auth-input"
            onChange={(e) => setEmail(e.target.value)} 
          />
          <input
            type="password"
            placeholder="סיסמה"
            className="auth-input"
            onChange={(e) => setPassword(e.target.value)}
          />
          <button
            type="button"
            className="btn-primary"
            style={{ marginTop: "10px" }}
            onClick={() => { 
              const myUser = userList.find(
                (user) => user.email === email && user.password === password
              );
              if (myUser) {
                enterUser(myUser);
              } else {
                alert("פרטי התחברות שגויים!");
              }
            }}
          >
            התחבר
          </button>
        </form>

        {/* 🌟 לחצן חזרה/מעבר להרשמה מעוצב ועדין */}
        <div style={{ marginTop: "20px", fontSize: "14px", color: "#666", textAlign: "center" }}>
          <span>עדיין אין לך חשבון? </span>
          <button 
            type="button" 
            onClick={() => navigate("/RegistrationSite")}
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
            להרשמה מהירה
          </button>
        </div>

      </div>
    </div>
  );
}