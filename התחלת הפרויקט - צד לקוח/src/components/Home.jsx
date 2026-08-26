// ראשי

import React from "react";
import { useNavigate } from "react-router-dom";

export default function Home() {
  let navigate = useNavigate();
  
  return (
    <div className="auth-wrapper">
      <div className="auth-card">
        <h1>ברוכים הבאים למערכת</h1>
        <p style={{ color: "#666", marginBottom: "30px" }}>אנא בחר את אפשרות הגישה שלך:</p>
        <div style={{ display: "flex", flexDirection: "column", gap: "15px" }}>
          <button
            className="btn-primary"
            onClick={() => navigate("/LoginSite")}
          >
            התחברות למערכת
          </button>
          <button
            className="btn-secondary"
            onClick={() => navigate("/RegistrationSite")}
          >
            יצירת חשבון חדש
          </button>
        </div>
      </div>
    </div>
  );
}