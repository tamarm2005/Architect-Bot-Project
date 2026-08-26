import React, { useContext, useEffect, useState, useRef } from 'react';
import { PlotContext } from './PlotContext';
import * as d3 from 'd3';


export default function BubbleDiagram({ onSendMessageToChat, selectedPlotPoints, onNavigateToFurniturePlan }) {
    const { programData, plotPoints } = useContext(PlotContext);
    const [nodes, setNodes] = useState([]);
    const [currentStage, setCurrentStage] = useState('bubble');
    const [isEditing, setIsEditing] = useState(false);
    const [isLoading, setIsLoading] = useState(false);
    const containerRef = useRef(null);

    // אפקט לאתחול הבועות
    useEffect(() => {
        if (!programData?.rooms || programData.rooms.length === 0) return;

        const containerWidth = containerRef.current ? containerRef.current.offsetWidth : 500;
        const containerHeight = containerRef.current ? containerRef.current.offsetHeight : 500;

        // 🏡 שולפים את נקודות המגרש (אם יש) כדי שהבועות יתחשבו בצורה/ביחס הרוחב-גובה שלו
        const rawVertices = programData?.plotVertices || programData?.plotPoints || [];
        const padding = 40;
        let bounds = { minX: padding, maxX: containerWidth - padding, minY: padding, maxY: containerHeight - padding };

        if (rawVertices.length >= 3) {
            const xs = rawVertices.map(p => Number(p.x || p.X || 0));
            const ys = rawVertices.map(p => Number(p.y || p.Y || 0));
            const plotMinX = Math.min(...xs), plotMaxX = Math.max(...xs);
            const plotMinY = Math.min(...ys), plotMaxY = Math.max(...ys);
            const plotW = plotMaxX - plotMinX || 1;
            const plotH = plotMaxY - plotMinY || 1;

            const availW = containerWidth - padding * 2;
            const availH = containerHeight - padding * 2;
            const scale = Math.min(availW / plotW, availH / plotH);
            const mappedW = plotW * scale;
            const mappedH = plotH * scale;
            const offsetX = (containerWidth - mappedW) / 2;
            const offsetY = (containerHeight - mappedH) / 2;

            bounds = { minX: offsetX, maxX: offsetX + mappedW, minY: offsetY, maxY: offsetY + mappedH };
        }

        const nodesData = programData.rooms.map(r => {
            const radius = Math.sqrt((Number(r.area) || 20) / Math.PI) * 12;
            return {
                ...r,
                id: String(r.id),
                radius: radius,
                x: (bounds.minX + bounds.maxX) / 2 + (Math.random() - 0.5) * 50,
                y: (bounds.minY + bounds.maxY) / 2 + (Math.random() - 0.5) * 50
            };
        });

        const validLinks = (programData.lines || [])
            .filter(link => nodesData.find(n => n.id === String(link.start)) && nodesData.find(n => n.id === String(link.end)))
            .map(link => ({ source: String(link.start), target: String(link.end) }));

        const simulation = d3.forceSimulation(nodesData)
            // דחייה מתונה בין כל הבועות - לא צמוד, לא נדחף לקצוות
            .force("charge", d3.forceManyBody().strength(-25))
            // התנגשות עם ריווח קטן נוסף (14px) כדי שלא ייגעו ממש אחת בשנייה
            .force("collide", d3.forceCollide().radius(d => d.radius + 6).strength(0.9))
            // משיכה עדינה למרכז שטח המגרש הממופה (לא למרכז המסך הסתמי)
            .force("center", d3.forceCenter((bounds.minX + bounds.maxX) / 2, (bounds.minY + bounds.maxY) / 2).strength(0.04));

        if (validLinks.length > 0) {
            // חדרים עם קשר שכנות נמשכים זה לזה, במרחק יחסי לגודל שלהם - לא מרחק קבוע
            simulation.force("link", d3.forceLink(validLinks)
                .id(d => d.id)
                .distance(l => (l.source.radius || 40) + (l.target.radius || 40) + 20)
                .strength(0.5)
            );
        }

        // 🧱 כוח גבולות - שלא תצא אף בועה מחוץ לצורת המגרש הממופה
        simulation.force("boundary", (alpha) => {
            for (const node of nodesData) {
                const r = node.radius;
                if (node.x - r < bounds.minX) node.x = bounds.minX + r;
                if (node.x + r > bounds.maxX) node.x = bounds.maxX - r;
                if (node.y - r < bounds.minY) node.y = bounds.minY + r;
                if (node.y + r > bounds.maxY) node.y = bounds.maxY - r;
            }
        });

        if (!isEditing) {
            simulation.on("tick", () => {
                setNodes([...nodesData]);
            });
        } else {
            setNodes(nodesData);
            simulation.stop();
        }

        return () => simulation.stop();
    }, [programData, isEditing]);

    const handleApproveBubble = async () => {
        setIsLoading(true);
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 25000); // 25 שניות מקסימום המתנה

        try {
            const scale = 35;

            const rawVertices = programData?.plotVertices || programData?.plotPoints || [];
            const plotVertices = rawVertices.map(p => ({
                X: Number(p.x || p.X || 0),
                Y: Number(p.y || p.Y || 0),
                Z: Number(p.z || p.Z || 0)
            }));

            const requestedRooms = (programData.rooms || []).map(room => {
                const matchedNode = nodes.find(n => String(n.id) === String(room.id));
                return {
                    ID: Number(room.id) || 0,
                    Name: room.name || "חדר",
                    Area: Number(room.area) || 20,
                    PositionX: (matchedNode ? matchedNode.x : 100) / scale,
                    PositionY: (matchedNode ? matchedNode.y : 100) / scale,
                    PreferredNeighbors: (programData.lines || [])
                        .filter(l => String(l.start) === String(room.id))
                        .map(l => programData.rooms.find(r => String(r.id) === String(l.end))?.name || "")
                        .filter(Boolean)
                };
            });

            const response = await fetch('http://localhost:5245/api/Cad/generate-layout', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                signal: controller.signal,
                body: JSON.stringify({
                    RequestedRooms: requestedRooms,
                    PlotVertices: plotVertices
                })
            });

            clearTimeout(timeoutId);
            if (!response.ok) {
                const errText = await response.text();
                throw new Error(errText || 'שגיאה בתקשורת עם השרת');
            }

            const layoutData = await response.json();
            onNavigateToFurniturePlan(layoutData);

        } catch (error) {
            clearTimeout(timeoutId);
            if (error.name === 'AbortError') {
                alert("⏱️ השרת (C#) לא הגיב תוך 25 שניות. בדקי שפרויקט ה-API רץ בפועל (Visual Studio / dotnet run) על פורט 5245, ושאין שגיאה אדומה בקונסולה שלו.");
            } else {
                console.error("שגיאה ביצירת הפריסה:", error);
                alert("קרתה שגיאה בייצור התוכנית:\n" + error.message);
            }
        } finally {
            setIsLoading(false);
        }
    };

    const handleRejectBubble = () => {
        const feedback = prompt("מה תרצה לתקן בסכמת הבועות?");
        if (feedback && onSendMessageToChat) {
            onSendMessageToChat(`נדרש תיקון לסכמת הבועות: ${feedback}`);
        }
    };

    const handleApproveFurniture = () => {
        setCurrentStage('complete');
        if (onSendMessageToChat) {
            onSendMessageToChat("תוכנית הריהוט מאושרת! אנא הכן את כל סט תוכניות העבודה להורדה.");
        }
    };

    const handleRejectFurniture = () => {
        const feedback = prompt("מה תרצה לתקן בתוכנית הריהוט?");
        if (feedback && onSendMessageToChat) {
            onSendMessageToChat(`נדרש תיקון לתוכנית הריהוט: ${feedback}`);
        }
    };

    return (
        <div style={{
            background: '#ffffff',
            borderRadius: '16px',
            border: '1px solid #e2e8f0',
            boxShadow: '0 4px 6px -1px rgba(0, 0, 0, 0.05), 0 2px 4px -1px rgba(0, 0, 0, 0.03)',
            padding: '24px',
            display: 'flex',
            flexDirection: 'column',
            height: '100%',
            boxSizing: 'border-box'
        }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px', flexWrap: 'wrap', gap: '12px' }}>
                <h3 style={{ margin: 0, fontSize: '18px', fontWeight: '700', color: '#1e293b', fontFamily: 'system-ui' }}>
                    {currentStage === 'bubble' && "📍 שלב 1: סכמת בועות וקרבת חדרים"}
                    {currentStage === 'furniture' && "🛋️ שלב 2: תוכנית ריהוט ראשונית"}
                    {currentStage === 'complete' && "📁 שלב 3: סט תוכניות עבודה סופי"}
                </h3>

                {currentStage === 'bubble' && (
                    <button
                        onClick={() => setIsEditing(!isEditing)}
                        style={{
                            marginRight: 'auto',
                            padding: '8px 14px',
                            cursor: 'pointer',
                            background: isEditing ? '#0ea5e9' : '#f1f5f9',
                            color: isEditing ? '#fff' : '#475569',
                            border: 'none',
                            borderRadius: '8px',
                            fontWeight: '600',
                            fontSize: '13px',
                            transition: 'all 0.2s'
                        }}
                    >
                        {isEditing ? '💾 שמור מיקומים' : '🎯 ערוך מיקומים ידנית'}
                    </button>
                )}
            </div>

            <div ref={containerRef} style={{
                flex: 1,
                position: 'relative',
                background: '#fafafa',
                backgroundImage: 'radial-gradient(#e2e8f0 1px, transparent 1px)',
                backgroundSize: '20px 20px',
                border: '1px solid #e2e8f0',
                borderRadius: '12px',
                overflow: 'hidden',
                minHeight: '400px'
            }}>
                {isLoading ? (
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', fontSize: '16px', color: '#0ea5e9', fontWeight: 'bold', padding: '20px', textAlign: 'center' }}>
                        ⏳ מנוע ה-C# מייצר כעת חלוקת קירות ומציב רהיטים בחישול מדומה...
                    </div>
                ) : currentStage === 'bubble' ? (
                    nodes.map((node, index) => (
                        <div
                            key={node.id && node.id !== "undefined" ? node.id : `bubble-${index}`}
                            onMouseDown={(e) => {
                                if (!isEditing) return;
                                const startX = e.clientX - node.x;
                                const startY = e.clientY - node.y;
                                const onMouseMove = (moveEvent) => {
                                    setNodes(prev => prev.map(n => n.id === node.id ? { ...n, x: moveEvent.clientX - startX, y: moveEvent.clientY - startY } : n));
                                };
                                const onMouseUp = () => {
                                    document.removeEventListener('mousemove', onMouseMove);
                                    document.removeEventListener('mouseup', onMouseUp);
                                };
                                document.addEventListener('mousemove', onMouseMove);
                                document.addEventListener('mouseup', onMouseUp);
                            }}
                            style={{
                                cursor: isEditing ? 'move' : 'default',
                                position: 'absolute',
                                left: `${node.x - node.radius}px`,
                                top: `${node.y - node.radius}px`,
                                width: `${node.radius * 2}px`,
                                height: `${node.radius * 2}px`,
                                borderRadius: '50%',
                                background: 'linear-gradient(135deg, #00b493 0%, #00876c 100%)',
                                color: '#ffffff',
                                display: 'flex',
                                flexDirection: 'column',
                                alignItems: 'center',
                                justifyContent: 'center',
                                fontSize: '13px',
                                fontWeight: '600',
                                textAlign: 'center',
                                boxShadow: '0 10px 15px -3px rgba(0, 135, 108, 0.3)',
                                transition: isEditing ? 'none' : 'left 0.1s, top 0.1s',
                                userSelect: 'none',
                                padding: '4px',
                                boxSizing: 'border-box'
                            }}
                        >
                            <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', width: '100%' }}>{node.name}</div>
                            <div style={{ fontSize: '10px', fontWeight: '400', opacity: 0.9, marginTop: '2px' }}>{node.area} מ"ר</div>
                        </div>
                    ))
                ) : currentStage === 'furniture' ? (
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', height: '100%', fontSize: '15px', color: '#64748b', fontWeight: '500' }}>
                        🖼️ כאן מוצגת תוכנית הריהוט הראשונית שחושבה באלגוריתם Simulated Annealing
                    </div>
                ) : (
                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '100%' }}>
                        <p style={{ fontSize: '16px', color: '#16a34a', fontWeight: '600', margin: '0 0 16px 0' }}>🎉 כל סט התוכניות האדריכליות מוכן!</p>
                        <button style={{ padding: '12px 24px', background: '#00a884', color: '#fff', border: 'none', borderRadius: '8px', cursor: 'pointer', fontWeight: '700', boxShadow: '0 4px 12px rgba(0, 168, 132, 0.3)' }}>
                            📥 הורד קובצי DXF ו-PDF של הפרויקט
                        </button>
                    </div>
                )}
            </div>

            <div style={{ marginTop: '20px', display: 'flex', gap: '12px', justifyContent: 'flex-end' }}>
                {currentStage === 'bubble' && !isLoading && (
                    <>
                        <button onClick={handleRejectBubble} style={{ padding: '10px 20px', background: '#ef4444', color: '#fff', border: 'none', borderRadius: '8px', cursor: 'pointer', fontWeight: '600', fontSize: '14px' }}>
                            ❌ תיקון סכמת בועות
                        </button>
                        <button onClick={handleApproveBubble} style={{ padding: '10px 20px', background: '#22c55e', color: '#fff', border: 'none', borderRadius: '8px', cursor: 'pointer', fontWeight: '600', fontSize: '14px' }}>
                            ✅ מאושר - המשך לריהוט
                        </button>
                    </>
                )}

                {currentStage === 'furniture' && (
                    <>
                        <button onClick={handleRejectFurniture} style={{ padding: '10px 20px', background: '#ef4444', color: '#fff', border: 'none', borderRadius: '8px', cursor: 'pointer', fontWeight: '600', fontSize: '14px' }}>
                            ❌ תיקון תוכנית ריהוט
                        </button>
                        <button onClick={handleApproveFurniture} style={{ padding: '10px 20px', background: '#22c55e', color: '#fff', border: 'none', borderRadius: '8px', cursor: 'pointer', fontWeight: '600', fontSize: '14px' }}>
                            ✅ מאושר - הפק סט סופי
                        </button>
                    </>
                )}
            </div>
        </div>
    );
}
