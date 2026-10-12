// התחברות

import React, { useState } from "react";
import { useNavigate } from "react-router-dom";

export default function LoginSite({ enterUser }) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  let navigate = useNavigate();

  const handleLogin = async () => {
    setIsLoading(true);
    try {
      const response = await fetch('http://localhost:5245/api/User/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password })
      });

      if (response.ok) {
        const data = await response.json();
        enterUser(data.user, data.token);
      } else {
        const errText = await response.text();
        alert(errText || "פרטי התחברות שגויים!");
      }
    } catch (error) {
      console.error("שגיאת התחברות:", error);
      alert("שגיאה בחיבור לשרת.");
    } finally {
      setIsLoading(false);
    }
  };

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
            disabled={isLoading}
            onClick={handleLogin}
          >
            {isLoading ? "מתחברת..." : "התחבר"}
          </button>
        </form>

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