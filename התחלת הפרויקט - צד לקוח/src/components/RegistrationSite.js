// הרשמה

import React, { useState } from "react";
import { useNavigate } from "react-router-dom";

export default function RegistrationSite({ enterUser }) {
  const [userName, setUserName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  let navigate = useNavigate();

  const handleRegister = async () => {
    setIsLoading(true);
    try {
      const response = await fetch('http://localhost:5245/api/User/register', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ username: userName, email, password })
      });

      if (response.ok) {
        const data = await response.json();
        alert("נרשמת בהצלחה!");
        enterUser(data.user, data.token);
      } else {
        const errText = await response.text();
        alert(errText || "שגיאה בהרשמה.");
      }
    } catch (error) {
      console.error("שגיאת הרשמה:", error);
      alert("שגיאה בחיבור לשרת.");
    } finally {
      setIsLoading(false);
    }
  };

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
            disabled={isLoading}
            onClick={handleRegister}
          >
            {isLoading ? "נרשמת..." : "הרשם והכנס"}
          </button>
        </form>

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