import React, { useContext, useState } from 'react';
import { PlotContext } from './PlotContext';
import CadChat from './CadChat';
import BubbleDiagram from './BubbleDiagram';
import FurniturePlan from './FurniturePlan';

export default function CadWorkspace({ currentUser, onLogout }) {
  const { programData } = useContext(PlotContext);
  const [furnitureLayoutData, setFurnitureLayoutData] = useState(null);
  const [isHistoryMode, setIsHistoryMode] = useState(false); // 🕒 מצב צפייה בשיחה ישנה

  const handleSendMessage = (text) => {
    if (window.sendChatUpdate) {
      window.sendChatUpdate(text);
    }
  };

  return (
    <div style={{
      display: 'flex', width: '100%', height: 'calc(100vh - 50px)',
      fontFamily: 'system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
      direction: 'rtl', background: '#f1f5f9', overflow: 'visible', position: 'relative'
    }}>
      <div style={{ width: '35%', height: '100%', borderLeft: '1px solid #e2e8f0', background: '#ffffff', display: 'flex', flexDirection: 'column' }}>
        <CadChat currentUser={currentUser} onHistoryModeChange={setIsHistoryMode} />
      </div>

      <div style={{ width: '65%', height: '100%', padding: '20px', boxSizing: 'border-box', overflowY: 'auto' }}>
        {isHistoryMode ? (
          // 📜 מצב תצוגת שיחה מהעבר - רק צ'אט, בלי שום שרטוט
          <div style={{
            display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center',
            height: '100%', color: '#64748b', fontSize: '15px', fontWeight: '500', textAlign: 'center'
          }}>
            📜 את צופה בשיחה מההיסטוריה. אין שרטוט פעיל עבורה - התחילי שיחה חדשה כדי לתכנן תוכנית.
          </div>
        ) : furnitureLayoutData ? (
          <FurniturePlan layoutData={furnitureLayoutData} onBack={() => setFurnitureLayoutData(null)} />
        ) : (
          <BubbleDiagram
            onSendMessageToChat={handleSendMessage}
            selectedPlotPoints={null}
            onNavigateToFurniturePlan={(data) => setFurnitureLayoutData(data)}
          />
        )}
      </div>
    </div>
  );
}