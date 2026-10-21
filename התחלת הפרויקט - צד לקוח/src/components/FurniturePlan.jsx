import React, { useContext, useEffect, useState, useRef } from 'react';
import { PlotContext } from './PlotContext';

const FurniturePlan = ({ layoutData, onBack }) => {
    const { rooms, totalAreaM2, coveragePercent } = layoutData;
    const { programData } = useContext(PlotContext);
    const containerRef = useRef(null);
    const [layout, setLayout] = useState({ scale: 20, offsetX: 0, offsetY: 0 });

    const [view, setView] = useState({ zoom: 1, panX: 0, panY: 0 });
    const isDragging = useRef(false);
    const lastMouse = useRef({ x: 0, y: 0 });

    const handleWheel = (e) => {
        e.preventDefault();
        const factor = e.deltaY < 0 ? 1.1 : 0.9;
        setView(prev => ({ ...prev, zoom: Math.max(0.3, Math.min(5, prev.zoom * factor)) }));
    };
    const handlePointerDown = (e) => {
        e.preventDefault();
        e.currentTarget.setPointerCapture(e.pointerId);
        isDragging.current = true;
        lastMouse.current = { x: e.clientX, y: e.clientY };
    };
    const handlePointerMove = (e) => {
        if (!isDragging.current) return;
        e.preventDefault();
        const dx = e.clientX - lastMouse.current.x;
        const dy = e.clientY - lastMouse.current.y;
        lastMouse.current = { x: e.clientX, y: e.clientY };
        setView(prev => ({ ...prev, panX: prev.panX + dx, panY: prev.panY + dy }));
    };
    const handlePointerUp = () => { isDragging.current = false; };

    const handleMouseDown = (e) => {
        isDragging.current = true;
        lastMouse.current = { x: e.clientX, y: e.clientY };
    };
    const handleMouseMove = (e) => {
        if (!isDragging.current) return;
        const dx = e.clientX - lastMouse.current.x;
        const dy = e.clientY - lastMouse.current.y;
        lastMouse.current = { x: e.clientX, y: e.clientY };
        setView(prev => ({ ...prev, panX: prev.panX + dx, panY: prev.panY + dy }));
    };
    const handleMouseUp = () => { isDragging.current = false; };

    // 🌟 מחשב אוטומטית את קנה המידה כדי שכל התוכנית תיכנס בתוך המסך, בלי גלילה
    useEffect(() => {
        if (!rooms || rooms.length === 0 || !containerRef.current) return;

        const wallPoints = layoutData.buildingOuterWall || [];
        const wallX = wallPoints.map(p => p.x ?? p.X ?? 0);
        const wallY = wallPoints.map(p => p.y ?? p.Y ?? 0);

        const allX = [...rooms.flatMap(r => [r.positionX || 0, (r.positionX || 0) + (r.width || 0)]), ...wallX];
        const allY = [...rooms.flatMap(r => [r.positionY || 0, (r.positionY || 0) + (r.length || 0)]), ...wallY];
        const minX = Math.min(...allX), maxX = Math.max(...allX);
        const minY = Math.min(...allY), maxY = Math.max(...allY);
        const planWidth = (maxX - minX) || 1;
        const planHeight = (maxY - minY) || 1;

        const padding = 30;
        const containerWidth = containerRef.current.offsetWidth;
        const containerHeight = containerRef.current.offsetHeight;

        const scaleX = (containerWidth - padding * 2) / planWidth;
        const scaleY = (containerHeight - padding * 2) / planHeight;
        const finalScale = Math.min(scaleX, scaleY);

        setLayout({ scale: finalScale, offsetX: -minX * finalScale + padding, offsetY: -minY * finalScale + padding });
    }, [rooms]);

    const { scale, offsetX, offsetY } = layout;

    const handleDownloadFullProject = async () => {
        try {
            const response = await fetch('http://localhost:5245/api/Cad/export-confirmed-layout-zip', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    BuildingOuterWall: layoutData.buildingOuterWall || [],
                    Rooms: (layoutData.rooms || []).map(r => ({
                        Name: r.name,
                        Width: r.width,
                        Length: r.length,
                        PositionX: r.positionX,
                        PositionY: r.positionY,
                        Furniture: (r.furniture || []).map(f => ({
                            Name: f.name,
                            Width: f.width,
                            Length: f.length,
                            X: f.x,
                            Y: f.y,
                            Rotation: f.rotation
                        }))
                    })),
                    Openings: (layoutData.openings || []).map(o => ({
                        RoomName: o.roomName ?? o.RoomName,
                        Type: o.type ?? o.Type,
                        X: o.x ?? o.X,
                        Y: o.y ?? o.Y,
                        Wall: o.wall ?? o.Wall,
                        Size: o.size ?? o.Size,
                        IsEntrance: !!(o.isEntrance ?? o.IsEntrance)
                    }))
                })
            });

            if (!response.ok) {
                const errText = await response.text();
                alert("שגיאה בהפקת קובץ ה-ZIP:\n" + errText);
                return;
            }

            const blob = await response.blob();
            const url = window.URL.createObjectURL(blob);
            const a = document.createElement('a');
            a.href = url;
            a.download = 'סט_תוכניות_אדריכלי.zip';
            document.body.appendChild(a);
            a.click();
            a.remove();
        } catch (error) {
            console.error("Error downloading ZIP:", error);
            alert("חלה שגיאה בחיבור לשרת בזמן יצירת התוכניות.");
        }
    };

    return (
        <div style={{ padding: '20px', fontFamily: 'Segoe UI, sans-serif', direction: 'rtl', height: '100%', boxSizing: 'border-box', display: 'flex', flexDirection: 'column' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px', background: '#ffffff', padding: '15px', borderRadius: '8px', boxShadow: '0 2px 4px rgba(0,0,0,0.05)' }}>
                <div>
                    <h2 style={{ margin: 0, color: '#1e293b' }}>🛋️ תוכנית העמדת ריהוט אופטימלית</h2>
                    <p style={{ margin: '5px 0 0 0', color: '#64748b', fontSize: '14px' }}>
                        שטח בנוי סופי: <strong>{totalAreaM2?.toFixed(2)} מ"ר</strong> | אחוז כיסוי מגרש: <strong>{coveragePercent?.toFixed(1)}%</strong>
                    </p>
                </div>

                <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
                    <div style={{ fontSize: '12px', color: '#64748b', textAlign: 'left', lineHeight: '1.2' }}>
                        <div>✔️ ת. ריהוט | ✔️ ת. מידות</div>
                        <div>✔️ חתכים | ✔️ חזיתות | ✔️ גגות</div>
                    </div>

                    <button
                        onClick={handleDownloadFullProject}
                        style={{ background: 'linear-gradient(135deg, #059669 0%, #00a884 100%)', border: 'none', padding: '10px 18px', borderRadius: '8px', cursor: 'pointer', fontWeight: '700', color: '#ffffff', boxShadow: '0 4px 6px rgba(0, 168, 132, 0.2)' }}
                    >
                        📥 הורד סט תוכניות מלא (ZIP)
                    </button>

                    <button
                        onClick={onBack}
                        style={{ background: '#f1f5f9', border: '1px solid #cbd5e1', padding: '10px 16px', borderRadius: '8px', cursor: 'pointer', fontWeight: '600', color: '#475569' }}
                    >
                        חזור לעריכת הבועות
                    </button>
                </div>
            </div>

            <div
                ref={containerRef}
                onWheel={handleWheel}
                onPointerDown={handlePointerDown}
                onPointerMove={handlePointerMove}
                onPointerUp={handlePointerUp}
                onPointerLeave={handlePointerUp}
                style={{
                    position: 'relative',
                    width: '100%',
                    flex: 1,
                    minHeight: '400px',
                    background: '#f8fafc',
                    border: '1px dashed #cbd5e1',
                    borderRadius: '12px',
                    overflow: 'hidden',
                    boxShadow: 'inset 0 2px 8px rgba(0,0,0,0.02)',
                    cursor: 'grab',
                    touchAction: 'none',
                    overscrollBehavior: 'contain'
                }}>
                <div style={{
                    width: '100%', height: '100%', position: 'relative',
                    transform: `translate(${view.panX}px, ${view.panY}px) scale(${view.zoom})`,
                    transformOrigin: 'center center'
                }}>
                    {layoutData.buildingOuterWall && layoutData.buildingOuterWall.length >= 3 && (() => {
                        const EXT = 0.20;                                   // קיר חיצוני 20 ס"מ
                        const wallThickness = Math.max(3, EXT * scale);
                        const xs = layoutData.buildingOuterWall.map(p => p.x ?? p.X);
                        const ys = layoutData.buildingOuterWall.map(p => p.y ?? p.Y);
                        const minXw = Math.min(...xs), maxXw = Math.max(...xs);
                        const minYw = Math.min(...ys), maxYw = Math.max(...ys);
                        const left = minXw * scale + offsetX;
                        const top = minYw * scale + offsetY;
                        const width = (maxXw - minXw) * scale;
                        const height = (maxYw - minYw) * scale;

                        // פתחים בקיר החיצוני: כל דלת שיושבת על קו המעטפת (כניסה, דלת למרפסת)
                        const pad = 2;
                        const gaps = (layoutData.openings || [])
                            .filter(o => (o.type || o.Type) === 'Door')
                            .map(o => {
                                const w = o.wall || o.Wall;
                                const ox = (o.x ?? o.X), oy = (o.y ?? o.Y);
                                const s = (o.size ?? o.Size ?? 0.9) * scale;
                                const px = ox * scale + offsetX, py = oy * scale + offsetY;
                                if (w === 'Left' || w === 'Right') {
                                    if (Math.abs(ox - minXw) < 0.35) return { left: left - pad, top: py - s / 2, width: wallThickness + 2 * pad, height: s };
                                    if (Math.abs(ox - maxXw) < 0.35) return { left: left + width - wallThickness - pad, top: py - s / 2, width: wallThickness + 2 * pad, height: s };
                                } else {
                                    if (Math.abs(oy - minYw) < 0.35) return { left: px - s / 2, top: top - pad, width: s, height: wallThickness + 2 * pad };
                                    if (Math.abs(oy - maxYw) < 0.35) return { left: px - s / 2, top: top + height - wallThickness - pad, width: s, height: wallThickness + 2 * pad };
                                }
                                return null;
                            })
                            .filter(Boolean);

                        return (
                            <>
                                {/* קיר חיצוני: רצועה בהירה עם שני קווים דקים (שיראו את העובי) */}
                                <div style={{
                                    position: 'absolute',
                                    left: `${left}px`, top: `${top}px`,
                                    width: `${width}px`, height: `${height}px`,
                                    border: `${wallThickness}px solid #cbd5e1`,
                                    boxSizing: 'border-box',
                                    boxShadow: '0 0 0 1.5px #0f172a, inset 0 0 0 1.5px #0f172a',
                                    pointerEvents: 'none',
                                    zIndex: 0
                                }} />
                                {gaps.map((g, i) => (
                                    <div key={`gap${i}`} style={{
                                        position: 'absolute',
                                        left: `${g.left}px`, top: `${g.top}px`,
                                        width: `${g.width}px`, height: `${g.height}px`,
                                        background: '#f8fafc',
                                        pointerEvents: 'none',
                                        zIndex: 1
                                    }} />
                                ))}
                            </>
                        );
                    })()}

                    {layoutData.openings?.map((o, i) => {
                        const wall = o.wall || o.Wall;
                        const isDoor = o.type === 'Door' || o.Type === 'Door';
                        const ox = (o.x ?? o.X) * scale + offsetX;
                        const oy = (o.y ?? o.Y) * scale + offsetY;
                        const s = (o.size ?? o.Size ?? 0.9) * scale;
                        const isVertical = wall === 'Left' || wall === 'Right';

                        if (isDoor) {
                            let boxLeft, boxTop, arcPath, wallLine, leafLine;
                            if (wall === 'Left') {
                                boxLeft = ox; boxTop = oy - s / 2;
                                wallLine = { x1: 0, y1: 0, x2: 0, y2: s };
                                leafLine = { x1: 0, y1: 0, x2: s, y2: 0 };
                                arcPath = `M 0 ${s} A ${s} ${s} 0 0 0 ${s} 0`;
                            } else if (wall === 'Right') {
                                boxLeft = ox - s; boxTop = oy - s / 2;
                                wallLine = { x1: s, y1: 0, x2: s, y2: s };
                                leafLine = { x1: s, y1: 0, x2: 0, y2: 0 };
                                arcPath = `M ${s} ${s} A ${s} ${s} 0 0 1 0 0`;
                            } else if (wall === 'Bottom') {
                                boxLeft = ox - s / 2; boxTop = oy;
                                wallLine = { x1: 0, y1: 0, x2: s, y2: 0 };
                                leafLine = { x1: 0, y1: 0, x2: 0, y2: s };
                                arcPath = `M ${s} 0 A ${s} ${s} 0 0 1 0 ${s}`;
                            } else {
                                boxLeft = ox - s / 2; boxTop = oy - s;
                                wallLine = { x1: 0, y1: s, x2: s, y2: s };
                                leafLine = { x1: s, y1: s, x2: s, y2: 0 };
                                arcPath = `M 0 ${s} A ${s} ${s} 0 0 1 ${s} 0`;
                            }
                            return (
                                <svg key={`opening-${i}`} width={s} height={s} style={{ position: 'absolute', left: `${boxLeft}px`, top: `${boxTop}px`, zIndex: 5, overflow: 'visible' }}>
                                    <line x1={wallLine.x1} y1={wallLine.y1} x2={wallLine.x2} y2={wallLine.y2} stroke="#b45309" strokeWidth="2" />
                                    <line x1={leafLine.x1} y1={leafLine.y1} x2={leafLine.x2} y2={leafLine.y2} stroke="#b45309" strokeWidth="1" strokeDasharray="2 2" />
                                    <path d={arcPath} fill="none" stroke="#b45309" strokeWidth="1" strokeDasharray="3 2" />
                                </svg>
                            );
                        }

                        const extT = Math.max(2, 0.2 * scale);   // עובי קיר חיצוני
                        const shift = extT / 2;                  // החלון באמצע עובי הקיר
                        const cx = wall === 'Left' ? ox - shift : wall === 'Right' ? ox + shift : ox;
                        const cy = wall === 'Bottom' ? oy - shift : wall === 'Top' ? oy + shift : oy;
                        return (
                            <div key={`opening-${i}`} style={{
                                position: 'absolute',
                                left: `${isVertical ? cx - extT / 2 : cx - s / 2}px`,
                                top: `${isVertical ? cy - s / 2 : cy - extT / 2}px`,
                                width: `${isVertical ? extT : s}px`,
                                height: `${isVertical ? s : extT}px`,
                                background: 'repeating-linear-gradient(' + (isVertical ? 'to bottom' : 'to right') + ', #38bdf8, #38bdf8 2px, white 2px, white 4px)',
                                border: '1px solid #0284c7',
                                boxSizing: 'border-box',
                                zIndex: 5
                            }} title="חלון" />
                        );
                    })}

                    {/* 🌟 סימוני מפלס - כניסה לבית (מבפנים ומבחוץ) ובתוך חדרי רטובים */}
                    {(() => {
                        const entrance = (layoutData.openings || []).find(o => o.isEntrance || o.IsEntrance);
                        const wetRooms = (rooms || []).filter(r => r.name && (r.name.includes('שירותים') || r.name.includes('רחצה') || r.name.includes('אמבטיה') || r.name.includes('מקלחת')));

                        const LevelMark = ({ x, y, label, key }) => (
                            <div key={key} style={{
                                position: 'absolute', left: `${x}px`, top: `${y}px`,
                                transform: 'translate(-50%, -50%)', zIndex: 6, textAlign: 'center', pointerEvents: 'none'
                            }}>
                                <svg width="16" height="16"><line x1="8" y1="0" x2="8" y2="16" stroke="#334155" strokeWidth="1" /><line x1="0" y1="8" x2="16" y2="8" stroke="#334155" strokeWidth="1" /></svg>
                                <div style={{ fontSize: '8px', color: '#334155', fontWeight: 600 }}>{label}</div>
                            </div>
                        );

                        const marks = [];
                        if (entrance) {
                            const ex = (entrance.x ?? entrance.X) * scale + offsetX;
                            const ey = (entrance.y ?? entrance.Y) * scale + offsetY;
                            const wall = entrance.wall || entrance.Wall;
                            const inward = wall === 'Left' ? [1, 0] : wall === 'Right' ? [-1, 0] : wall === 'Bottom' ? [0, 1] : [0, -1];
                            const d = 25;
                            marks.push(<LevelMark key="in" x={ex + inward[0] * d} y={ey + inward[1] * d} label="±0.00" />);
                            marks.push(<LevelMark key="out" x={ex - inward[0] * d} y={ey - inward[1] * d} label="±0.00" />);
                        }
                        wetRooms.forEach((r, idx) => {
                            const cx = (r.positionX + r.width / 2) * scale + offsetX;
                            const cy = (r.positionY + r.length / 2) * scale + offsetY;
                            marks.push(<LevelMark key={`wet${idx}`} x={cx} y={cy} label="-0.02" />);
                        });
                        return marks;
                    })()}

                    {rooms?.map((room, roomIdx) => {
                        const isCorridor = room.name === 'מסדרון';
                        const roomLeft = (room.positionX || 0) * scale + offsetX;
                        const roomTop = (room.positionY || 0) * scale + offsetY;
                        const roomW = room.width * scale;
                        const roomH = room.length * scale;


                        // כל הדלתות בבניין (כדי שגם החדר השכן יחתוך פתח בקיר שלו)
                        const allDoors = (layoutData.openings || []).filter(o => (o.type || o.Type) === 'Door');

                        const wallSegments = (start, end, gaps) => {
                            let segs = [[start, end]];
                            gaps.forEach(([gs, ge]) => {
                                const next = [];
                                segs.forEach(([s, e]) => {
                                    if (ge <= s || gs >= e) { next.push([s, e]); return; }
                                    if (gs > s) next.push([s, gs]);
                                    if (ge < e) next.push([ge, e]);
                                });
                                segs = next;
                            });
                            return segs;
                        };

                        const gapsFor = (wallName) => {
                            const vertical = wallName === 'Left' || wallName === 'Right';
                            const edge = wallName === 'Left' ? room.positionX
                                : wallName === 'Right' ? room.positionX + room.width
                                    : wallName === 'Bottom' ? room.positionY
                                        : room.positionY + room.length;
                            return allDoors.filter(d => {
                                const dw = d.wall || d.Wall;
                                const dVertical = dw === 'Left' || dw === 'Right';
                                if (dVertical !== vertical) return false;
                                const coord = vertical ? (d.x ?? d.X) : (d.y ?? d.Y);
                                if (Math.abs(coord - edge) > 0.25) return false;
                                const along = vertical ? (d.y ?? d.Y) : (d.x ?? d.X);
                                const lo = vertical ? room.positionY : room.positionX;
                                const hi = vertical ? room.positionY + room.length : room.positionX + room.width;
                                return along >= lo && along <= hi;
                            }).map(d => {
                                const dx = (d.x ?? d.X) * scale + offsetX;
                                const dy = (d.y ?? d.Y) * scale + offsetY;
                                const s = (d.size ?? d.Size ?? 0.9) * scale;
                                return vertical ? [dy - s / 2, dy + s / 2] : [dx - s / 2, dx + s / 2];
                            });
                        };

                        const leftSegs = wallSegments(roomTop, roomTop + roomH, gapsFor('Left'));
                        const rightSegs = wallSegments(roomTop, roomTop + roomH, gapsFor('Right'));
                        const bottomWallSegs = wallSegments(roomLeft, roomLeft + roomW, gapsFor('Bottom')); // עולם Y קטן = חלק עליון בפיקסלים
                        const topWallSegs = wallSegments(roomLeft, roomLeft + roomW, gapsFor('Top'));       // עולם Y גדול = חלק תחתון בפיקסלים
                        const wallT = Math.max(1.5, 0.05 * scale);   // כל חדר מצייר חצי קיר (5 ס"מ), יחד 10 ס"מ

                        const isBalcony = (room.name || '').includes('מרפסת');
                        // איזה צדדים של המרפסת פונים החוצה (שם יש מעקה) ואיזה נוגע במעטפת הבניין
                        const envPts = layoutData.buildingOuterWall || [];
                        const envXs = envPts.map(p => p.x ?? p.X), envYs = envPts.map(p => p.y ?? p.Y);
                        const eMinX = Math.min(...envXs), eMaxX = Math.max(...envXs);
                        const eMinY = Math.min(...envYs), eMaxY = Math.max(...envYs);
                        const rx0 = room.positionX, rx1 = room.positionX + room.width;
                        const ry0 = room.positionY, ry1 = room.positionY + room.length;
                        const T = 0.35;
                        const railLeft = isBalcony && !(Math.abs(rx0 - eMaxX) < T);
                        const railRight = isBalcony && !(Math.abs(rx1 - eMinX) < T);
                        const railTop = isBalcony && !(Math.abs(ry0 - eMaxY) < T);
                        const railBottom = isBalcony && !(Math.abs(ry1 - eMinY) < T);
                        const railIn = Math.max(2.5, 0.05 * scale);
                        const isCovered = isBalcony && (room.name || '').includes('מקורה');
                        const colSize = Math.max(3, 0.2 * scale);
                        
                        return (
                            <div key={roomIdx} style={{ position: 'absolute', left: `${roomLeft}px`, top: `${roomTop}px`, width: `${roomW}px`, height: `${roomH}px` }}>
                                <div style={{ position: 'absolute', inset: 0, background: isCovered ? 'repeating-linear-gradient(45deg, #ffffff, #ffffff 6px, #e2e8f0 6px, #e2e8f0 7px)' : isCorridor ? 'rgba(148,163,184,0.08)' : '#ffffff', boxShadow: '0 4px 6px rgba(0,0,0,0.02)' }} />
                                <svg width={roomW} height={roomH} style={{ position: 'absolute', top: 0, left: 0, overflow: 'visible', pointerEvents: 'none' }}>
                                    {!isCorridor && !isBalcony && leftSegs.map(([s, e], i) => (
                                        <g key={`l${i}`}>
                                            <line x1={-wallT / 2} y1={s - roomTop} x2={-wallT / 2} y2={e - roomTop} stroke="#cbd5e1" strokeWidth={wallT} />
                                            <line x1={0} y1={s - roomTop} x2={0} y2={e - roomTop} stroke="#1e293b" strokeWidth={1.2} />
                                        </g>
                                    ))}
                                    {!isCorridor && !isBalcony && rightSegs.map(([s, e], i) => (
                                        <g key={`r${i}`}>
                                            <line x1={roomW + wallT / 2} y1={s - roomTop} x2={roomW + wallT / 2} y2={e - roomTop} stroke="#cbd5e1" strokeWidth={wallT} />
                                            <line x1={roomW} y1={s - roomTop} x2={roomW} y2={e - roomTop} stroke="#1e293b" strokeWidth={1.2} />
                                        </g>
                                    ))}
                                    {!isCorridor && !isBalcony && bottomWallSegs.map(([s, e], i) => (
                                        <g key={`b${i}`}>
                                            <line x1={s - roomLeft} y1={-wallT / 2} x2={e - roomLeft} y2={-wallT / 2} stroke="#cbd5e1" strokeWidth={wallT} />
                                            <line x1={s - roomLeft} y1={0} x2={e - roomLeft} y2={0} stroke="#1e293b" strokeWidth={1.2} />
                                        </g>
                                    ))}
                                    {!isCorridor && !isBalcony && topWallSegs.map(([s, e], i) => (
                                        <g key={`t${i}`}>
                                            <line x1={s - roomLeft} y1={roomH + wallT / 2} x2={e - roomLeft} y2={roomH + wallT / 2} stroke="#cbd5e1" strokeWidth={wallT} />
                                            <line x1={s - roomLeft} y1={roomH} x2={e - roomLeft} y2={roomH} stroke="#1e293b" strokeWidth={1.2} />
                                        </g>
                                    ))}

                                    {/* מרפסת מקורה: עמודי 20x20 בפינות החיצוניות */}
                                    {isCovered && [[0, 0], [1, 0], [0, 1], [1, 1]]
                                        .filter(([a, b]) => !((a === 0 && !railLeft) || (a === 1 && !railRight) || (b === 0 && !railTop) || (b === 1 && !railBottom)))
                                        .map(([a, b], k) => (
                                            <rect key={`col${k}`} x={a ? roomW - colSize : 0} y={b ? roomH - colSize : 0} width={colSize} height={colSize} fill="#334155" />
                                        ))}

                                    {/* מרפסת: מעקה (שני קווים דקים) רק בצדדים שפונים החוצה */}                                    {railLeft && <g><line x1={0} y1={0} x2={0} y2={roomH} stroke="#0f172a" strokeWidth={2} /><line x1={railIn} y1={0} x2={railIn} y2={roomH} stroke="#64748b" strokeWidth={1} /></g>}
                                    {railRight && <g><line x1={roomW} y1={0} x2={roomW} y2={roomH} stroke="#0f172a" strokeWidth={2} /><line x1={roomW - railIn} y1={0} x2={roomW - railIn} y2={roomH} stroke="#64748b" strokeWidth={1} /></g>}
                                    {railTop && <g><line x1={0} y1={0} x2={roomW} y2={0} stroke="#0f172a" strokeWidth={2} /><line x1={0} y1={railIn} x2={roomW} y2={railIn} stroke="#64748b" strokeWidth={1} /></g>}
                                    {railBottom && <g><line x1={0} y1={roomH} x2={roomW} y2={roomH} stroke="#0f172a" strokeWidth={2} /><line x1={0} y1={roomH - railIn} x2={roomW} y2={roomH - railIn} stroke="#64748b" strokeWidth={1} /></g>}
                                </svg>

                                <div style={{
                                    position: 'absolute', top: '3px', right: '3px', fontSize: '10px', fontWeight: 'bold',
                                    color: '#475569', background: 'rgba(241, 245, 249, 0.85)', padding: '1px 4px', borderRadius: '4px', zIndex: 10,
                                    whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis', maxWidth: '95%'
                                }}>
                                    {room.name} ({room.area?.toFixed(1) || (room.width * room.length).toFixed(1)} מ"ר)
                                </div>
                                {room.furniture?.map((f, furnitureIdx) => {
                                    const isRotated90 = f.rotation === 90 || f.rotation === 270;
                                    const fWidth = (isRotated90 ? f.length : f.width) * scale;
                                    const fLength = (isRotated90 ? f.width : f.length) * scale;
                                    return (
                                        <div key={furnitureIdx} style={{
                                            position: 'absolute',
                                            left: `${(f.x || 0) * scale}px`,
                                            top: `${(f.y || 0) * scale}px`,
                                            width: `${fWidth}px`,
                                            height: `${fLength}px`,
                                            background: 'rgba(14, 165, 233, 0.15)',
                                            border: '1px solid #0284c7',
                                            borderRadius: '2px',
                                            display: 'flex', alignItems: 'center', justifyContent: 'center',
                                            fontSize: '9px', fontWeight: '600', color: '#0369a1', textAlign: 'center', boxSizing: 'border-box',
                                        }} title={`סיבוב: ${f.rotation || 0}°`}>
                                            {f.name}
                                        </div>
                                    );
                                })}
                            </div>
                        );
                    })}
                </div>
            </div>
        </div>
    );
};
export default FurniturePlan;