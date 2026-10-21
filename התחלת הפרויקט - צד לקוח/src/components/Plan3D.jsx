import React, { useEffect, useMemo, useRef, useState } from 'react';
import * as THREE from 'three';
import { Canvas, useFrame, useThree } from '@react-three/fiber';
import { OrbitControls } from '@react-three/drei';

const EXT_T = 0.2;       // קיר חיצוני
const HALF_T = 0.05;     // חצי מחיצה (כל חדר בונה חצי), יחד 10 ס"מ
const WALL_H = 2.6;      // גובה נקי
const HALF_H = 1.1;      // גובה קירות במצב "בית בובות"
const SLAB_T = 0.2;
const PARAPET_H = 0.6;
const BAL_STEP = 0.15;   // רצפת מרפסת מתחת לרצפת הדירה
const HEAD = 2.1;

const WET_RE = /שירותים|אמבט|רחצה|מקלח|כביסה/;

function furnHeight(n = '') {
    if (n.includes('מקרר')) return 1.8;
    if (n.includes('ספריה')) return 1.8;
    if (n.includes('מטבח') || n.includes('כיור') || n.includes('כיריים') || n.includes('תנור')) return 0.9;
    if (n.includes('ארון')) return 2.2;
    if (n.includes('מיטה') || n.includes('מזרן')) return 0.5;
    if (n.includes('ספה')) return 0.85;
    if (n.includes('שולחן')) return 0.75;
    if (n.includes('כיסא')) return 0.9;
    if (n.includes('אסלה')) return 0.45;
    if (n.includes('אמבט')) return 0.55;
    if (n.includes('מקלחון') || n.includes('מקלחת')) return 2.0;
    if (n.includes('מכונת')) return 0.85;
    if (n.includes('שידה') || n.includes('מזנון') || n.includes('טלוויזיה')) return 0.55;
    return 0.75;
}

function kindOf(n = '') {
    if (n.includes('מיטה')) return 'bed';
    if (n.includes('ספה')) return 'sofa';
    if (n.includes('ארון מטבח')) return 'kcabinet';
    if (n.includes('ספריה')) return 'bookcase';
    if (n.includes('ארון')) return 'wardrobe';
    if (n.includes('מקרר')) return 'fridge';
    if (n.includes('כיריים') || n.includes('תנור')) return 'stove';
    if (n.includes('כיור מטבח')) return 'ksink';
    if (n.includes('כיור')) return 'sink';
    if (n.includes('אסלה')) return 'toilet';
    if (n.includes('אמבט')) return 'tub';
    if (n.includes('מקלחון') || n.includes('מקלחת')) return 'shower';
    if (n.includes('מכונת')) return 'washer';
    if (n.includes('שולחן')) return 'table';
    if (n.includes('כיסא')) return 'chair';
    if (n.includes('שידה') || n.includes('מזנון') || n.includes('טלוויזיה')) return 'dresser';
    return 'box';
}

function floorKind(n = '') {
    if (WET_RE.test(n) || n.includes('מטבח')) return 'tile';
    if (n.includes('מסדרון') || n.includes('מרפסת')) return 'plain';
    return 'wood';
}

function makeFloorTexture(kind, w, d) {
    const c = document.createElement('canvas');
    c.width = 256; c.height = 256;
    const g = c.getContext('2d');
    if (kind === 'wood') {
        for (let i = 0; i < 4; i++) {
            const shade = 196 + ((i * 37) % 24);
            g.fillStyle = `rgb(${shade}, ${shade - 28}, ${shade - 70})`;
            g.fillRect(0, i * 64, 256, 64);
            g.fillStyle = 'rgba(80,50,20,0.35)';
            g.fillRect(0, i * 64, 256, 2);
            g.fillRect(((i * 97) % 200) + 20, i * 64, 2, 64);
        }
    } else {
        g.fillStyle = '#eef3f6'; g.fillRect(0, 0, 256, 256);
        g.fillStyle = '#b9c6cf';
        g.fillRect(0, 0, 256, 3); g.fillRect(0, 126, 256, 3); g.fillRect(0, 0, 3, 256); g.fillRect(126, 0, 3, 256);
    }
    const t = new THREE.CanvasTexture(c);
    t.wrapS = t.wrapT = THREE.RepeatWrapping;
    t.repeat.set(Math.max(1, w), Math.max(1, d));
    if (THREE.SRGBColorSpace) t.colorSpace = THREE.SRGBColorSpace;
    return t;
}

// מחלק קיר לאורכו למלבנים סביב הפתחים: מלא, מתחת לאדן, מעל המשקוף
function wallRects(a0, a1, opens, H) {
    const sorted = opens
        .map(o => ({ lo: Math.max(a0, o.c - o.w / 2), hi: Math.min(a1, o.c + o.w / 2), sill: Math.min(o.sill, H), head: Math.min(o.head, H) }))
        .filter(o => o.hi - o.lo > 0.05)
        .sort((p, q) => p.lo - q.lo);
    const rects = [];
    let cur = a0;
    for (const o of sorted) {
        if (o.lo > cur) rects.push({ u0: cur, u1: o.lo, y0: 0, y1: H });
        if (o.sill > 0.01) rects.push({ u0: o.lo, u1: o.hi, y0: 0, y1: o.sill });
        if (o.head < H - 0.01) rects.push({ u0: o.lo, u1: o.hi, y0: o.head, y1: H });
        cur = Math.max(cur, o.hi);
    }
    if (cur < a1) rects.push({ u0: cur, u1: a1, y0: 0, y1: H });
    return rects;
}

function buildScene(layoutData, H) {
    const rooms = (layoutData.rooms || []).map(r => ({
        name: r.name || '',
        x0: r.positionX || 0, y0: r.positionY || 0,
        x1: (r.positionX || 0) + (r.width || 0), y1: (r.positionY || 0) + (r.length || 0),
        furniture: r.furniture || []
    }));

    const outline = layoutData.buildingOuterWall || [];
    const ox = outline.map(p => p.x ?? p.X), oy = outline.map(p => p.y ?? p.Y);
    const has = ox.length >= 3;
    const minX = has ? Math.min(...ox) : Math.min(...rooms.map(r => r.x0)) - EXT_T;
    const maxX = has ? Math.max(...ox) : Math.max(...rooms.map(r => r.x1)) + EXT_T;
    const minY = has ? Math.min(...oy) : Math.min(...rooms.map(r => r.y0)) - EXT_T;
    const maxY = has ? Math.max(...oy) : Math.max(...rooms.map(r => r.y1)) + EXT_T;

    const openings = (layoutData.openings || []).map(o => ({
        wall: o.wall ?? o.Wall, type: o.type ?? o.Type,
        x: o.x ?? o.X, y: o.y ?? o.Y,
        size: o.size ?? o.Size ?? 0.9,
        room: o.roomName ?? o.RoomName ?? '',
        entrance: !!(o.isEntrance ?? o.IsEntrance)
    }));
    const isH = o => o.wall === 'Top' || o.wall === 'Bottom';
    const sillOf = o => (o.type === 'Door' ? 0 : (WET_RE.test(o.room) ? 1.5 : (o.size >= 1.8 ? 0.45 : 0.9)));

    // ----- מרפסות בולטות -----
    const balconyDir = r => {
        const cx = (r.x0 + r.x1) / 2, cy = (r.y0 + r.y1) / 2;
        const d = { S: cy - minY, N: maxY - cy, E: maxX - cx, W: cx - minX };
        return Object.keys(d).reduce((a, b) => (d[b] < d[a] ? b : a));
    };
    const outerRect = (r, d) => {
        let { x0, x1, y0, y1 } = r;
        if (d === 'S') y1 = Math.min(y1, minY);
        else if (d === 'N') y0 = Math.max(y0, maxY);
        else if (d === 'E') x0 = Math.max(x0, maxX);
        else x1 = Math.min(x1, minX);
        return { x0, x1, y0, y1 };
    };
    rooms.forEach(r => {
        r.bal = null;
        if (!r.name.includes('מרפסת')) return;
        const d = balconyDir(r), rc = outerRect(r, d);
        if (rc.x1 - rc.x0 > 0.05 && rc.y1 - rc.y0 > 0.05) r.bal = { d, rc, covered: r.name.includes('מקורה') };
    });

    const walls = [], glass = [], frames = [], doors = [], floors = [], furn = [], rails = [], roof = [], labels = [];

    const addBox = (list, horiz, fixed, c, y, len, hh, depth, color) => {
        list.push(horiz ? { pos: [c, y, fixed], size: [len, hh, depth], color } : { pos: [fixed, y, c], size: [depth, hh, len], color });
    };

    const pushWall = (isHoriz, fixed, a0, a1, t, opens, color) => {
        wallRects(a0, a1, opens, H).forEach(r => {
            const len = r.u1 - r.u0, hh = r.y1 - r.y0, mid = (r.u0 + r.u1) / 2, cy = (r.y0 + r.y1) / 2;
            addBox(walls, isHoriz, fixed, mid, cy, len, hh, t, color);
        });
    };

    const sides = [
        { h: true, line: minY, fixed: minY + EXT_T / 2, a0: minX, a1: maxX },
        { h: true, line: maxY, fixed: maxY - EXT_T / 2, a0: minX, a1: maxX },
        { h: false, line: minX, fixed: minX + EXT_T / 2, a0: minY + EXT_T, a1: maxY - EXT_T },
        { h: false, line: maxX, fixed: maxX - EXT_T / 2, a0: minY + EXT_T, a1: maxY - EXT_T }
    ];

    // ----- קירות חיצוניים (20 ס"מ) + חלונות עם מסגרת וזכוכית -----
    sides.forEach(s => {
        const opens = openings
            .filter(o => isH(o) === s.h && Math.abs((s.h ? o.y : o.x) - s.line) < 0.35)
            .map(o => ({ c: s.h ? o.x : o.y, w: o.size, sill: sillOf(o), head: HEAD, door: o.type === 'Door' }));
        pushWall(s.h, s.fixed, s.a0, s.a1, EXT_T, opens, '#e2e4e8');
        opens.filter(o => !o.door).forEach(o => {
            const top = Math.min(o.head, H), gh = top - o.sill;
            if (gh < 0.15) return;
            const y = o.sill + gh / 2;
            addBox(glass, s.h, s.fixed, o.c, y, o.w, gh, 0.03, '#7dd3fc');
            addBox(frames, s.h, s.fixed, o.c, o.sill + 0.02, o.w, 0.04, EXT_T + 0.02, '#f8fafc');
            addBox(frames, s.h, s.fixed, o.c, top - 0.02, o.w, 0.04, EXT_T + 0.02, '#f8fafc');
            addBox(frames, s.h, s.fixed, o.c - o.w / 2 + 0.02, y, 0.04, gh, EXT_T + 0.02, '#f8fafc');
            addBox(frames, s.h, s.fixed, o.c + o.w / 2 - 0.02, y, 0.04, gh, EXT_T + 0.02, '#f8fafc');
            addBox(frames, s.h, s.fixed, o.c, y, 0.03, gh, 0.06, '#f8fafc');
        });
    });

    // ----- מחיצות פנימיות: כל חדר בונה את חצי המחיצה שלו -----
    rooms.forEach(r => {
        if (r.bal) return;
        const edges = [
            { h: true, line: r.y0, fixed: r.y0 - HALF_T / 2, a0: r.x0 - HALF_T, a1: r.x1 + HALF_T, env: Math.abs(r.y0 - (minY + EXT_T)) < 0.12, lo: r.x0, hi: r.x1 },
            { h: true, line: r.y1, fixed: r.y1 + HALF_T / 2, a0: r.x0 - HALF_T, a1: r.x1 + HALF_T, env: Math.abs(r.y1 - (maxY - EXT_T)) < 0.12, lo: r.x0, hi: r.x1 },
            { h: false, line: r.x0, fixed: r.x0 - HALF_T / 2, a0: r.y0 - HALF_T, a1: r.y1 + HALF_T, env: Math.abs(r.x0 - (minX + EXT_T)) < 0.12, lo: r.y0, hi: r.y1 },
            { h: false, line: r.x1, fixed: r.x1 + HALF_T / 2, a0: r.y0 - HALF_T, a1: r.y1 + HALF_T, env: Math.abs(r.x1 - (maxX - EXT_T)) < 0.12, lo: r.y0, hi: r.y1 }
        ];
        edges.forEach(e => {
            if (e.env) return;
            const opens = openings
                .filter(o => o.type === 'Door' && isH(o) === e.h && Math.abs((e.h ? o.y : o.x) - e.line) < 0.25)
                .filter(o => { const along = e.h ? o.x : o.y; return along >= e.lo - 0.05 && along <= e.hi + 0.05; })
                .map(o => ({ c: e.h ? o.x : o.y, w: o.size, sill: 0, head: HEAD }));
            pushWall(e.h, e.fixed, e.a0, e.a1, HALF_T, opens, '#fafaf8');
        });
    });

    // ----- דלתות (כנף + משקוף; מרפסת = הזזה מזכוכית) -----
    openings.filter(o => o.type === 'Door').forEach(o => {
        const horiz = isH(o);
        const side = sides.find(s => s.h === horiz && Math.abs((horiz ? o.y : o.x) - s.line) < 0.35);
        const own = rooms.find(r => r.name === o.room);
        const rc = own ? { x: (own.x0 + own.x1) / 2, z: (own.y0 + own.y1) / 2 } : { x: o.x, z: o.y };
        const kind = o.entrance ? 'entrance' : (o.room.includes('מרפסת') ? 'balcony' : 'interior');
        doors.push({
            kind, horiz, w: o.size, t: side ? EXT_T : HALF_T * 2,
            x: horiz ? o.x : (side ? side.fixed : o.x),
            z: horiz ? (side ? side.fixed : o.y) : o.y,
            roomC: rc
        });
    });

    // ----- רצפות עם מרקם -----
    floors.push({ pos: [(minX + maxX) / 2, -0.11, (minY + maxY) / 2], size: [maxX - minX, 0.2, maxY - minY], color: '#aeb6bf', kind: 'plain' });
    rooms.forEach(r => {
        if (r.bal) return;
        floors.push({ pos: [(r.x0 + r.x1) / 2, 0, (r.y0 + r.y1) / 2], size: [r.x1 - r.x0, 0.02, r.y1 - r.y0], color: '#d9dce1', kind: floorKind(r.name) });
        if (!r.name.includes('מסדרון')) labels.push({ x: (r.x0 + r.x1) / 2, z: (r.y0 + r.y1) / 2, text: r.name });
    });

    // ----- ריהוט: הגב פונה אל הקיר הקרוב ביותר -----
    rooms.forEach(r => r.furniture.forEach(f => {
        const rot = (((f.rotation || 0) % 180) + 180) % 180;
        const sw = rot > 45 && rot < 135;
        const fw = (sw ? f.length : f.width) || 0.5, fl = (sw ? f.width : f.length) || 0.5;
        const fx0 = r.x0 + (f.x || 0), fy0 = r.y0 + (f.y || 0);
        const cx = fx0 + fw / 2, cz = fy0 + fl / 2;
        const gaps = [
            { k: 'xm', v: fx0 - r.x0 }, { k: 'xp', v: r.x1 - (fx0 + fw) },
            { k: 'zm', v: fy0 - r.y0 }, { k: 'zp', v: r.y1 - (fy0 + fl) }
        ];
        const near = gaps.reduce((a, b) => (b.v < a.v ? b : a)).k;
        let W = fw, D = fl, yaw = 0;
        if (near === 'zp') yaw = Math.PI;
        else if (near === 'xm') { yaw = Math.PI / 2; W = fl; D = fw; }
        else if (near === 'xp') { yaw = -Math.PI / 2; W = fl; D = fw; }
        furn.push({ name: f.name || '', kind: kindOf(f.name), cx, cz, W, D, yaw, h: furnHeight(f.name), w: fw, d: fl });
    }));

    // ----- מרפסות: רצפה, מעקה עם סורגים, ובמקורה תקרה ועמודים -----
    rooms.forEach(r => {
        if (!r.bal) return;
        const { d, rc, covered } = r.bal;
        const w = rc.x1 - rc.x0, dd = rc.y1 - rc.y0, cx = (rc.x0 + rc.x1) / 2, cz = (rc.y0 + rc.y1) / 2;
        floors.push({ pos: [cx, -BAL_STEP - 0.1, cz], size: [w, 0.2, dd], color: '#b9c0c8', kind: 'plain' });
        labels.push({ x: cx, z: cz, text: r.name });

        const top = -BAL_STEP;
        [
            { skip: d === 'N', a: [rc.x0, rc.y0], b: [rc.x1, rc.y0] },
            { skip: d === 'S', a: [rc.x0, rc.y1], b: [rc.x1, rc.y1] },
            { skip: d === 'E', a: [rc.x0, rc.y0], b: [rc.x0, rc.y1] },
            { skip: d === 'W', a: [rc.x1, rc.y0], b: [rc.x1, rc.y1] }
        ].forEach(s => {
            if (s.skip) return;
            const [ax, az] = s.a, [bx, bz] = s.b;
            const len = Math.hypot(bx - ax, bz - az), dx = (bx - ax) / len, dz = (bz - az) / len;
            const horiz = Math.abs(dz) < 1e-6, mx = (ax + bx) / 2, mz = (az + bz) / 2;
            rails.push({ pos: [mx, top + 0.975, mz], size: horiz ? [len, 0.05, 0.06] : [0.06, 0.05, len], color: '#475569' });
            rails.push({ pos: [mx, top + 0.08, mz], size: horiz ? [len, 0.03, 0.04] : [0.04, 0.03, len], color: '#475569' });
            for (let u = 0.06; u < len; u += 0.12)
                rails.push({ pos: [ax + dx * u, top + 0.54, az + dz * u], size: [0.02, 0.92, 0.02], color: '#475569' });
        });

        if (covered) {
            roof.push({ pos: [cx, WALL_H + SLAB_T / 2, cz], size: [w, SLAB_T, dd], color: '#c9ced4' });
            [[rc.x0, rc.y0], [rc.x1, rc.y0], [rc.x0, rc.y1], [rc.x1, rc.y1]].forEach(([px, pz]) => {
                const onWall = (d === 'S' && pz === rc.y1) || (d === 'N' && pz === rc.y0) || (d === 'E' && px === rc.x0) || (d === 'W' && px === rc.x1);
                if (onWall) return;
                const colH = WALL_H - top;
                walls.push({ pos: [px === rc.x0 ? px + 0.1 : px - 0.1, top + colH / 2, pz === rc.y0 ? pz + 0.1 : pz - 0.1], size: [0.2, colH, 0.2], color: '#d4d8dd' });
            });
        }
    });

    // ----- תקרה ופרפט -----
    const W = maxX - minX, D = maxY - minY;
    roof.push({ pos: [(minX + maxX) / 2, WALL_H + SLAB_T / 2, (minY + maxY) / 2], size: [W, SLAB_T, D], color: '#c9ced4' });
    const py = WALL_H + SLAB_T + PARAPET_H / 2;
    roof.push({ pos: [(minX + maxX) / 2, py, minY + EXT_T / 2], size: [W, PARAPET_H, EXT_T], color: '#e2e4e8' });
    roof.push({ pos: [(minX + maxX) / 2, py, maxY - EXT_T / 2], size: [W, PARAPET_H, EXT_T], color: '#e2e4e8' });
    roof.push({ pos: [minX + EXT_T / 2, py, (minY + maxY) / 2], size: [EXT_T, PARAPET_H, D - 2 * EXT_T], color: '#e2e4e8' });
    roof.push({ pos: [maxX - EXT_T / 2, py, (minY + maxY) / 2], size: [EXT_T, PARAPET_H, D - 2 * EXT_T], color: '#e2e4e8' });

    // ----- גבולות, התנגשויות לצורך הליכה, ונקודת התחלה -----
    let bx0 = minX, bx1 = maxX, bz0 = minY, bz1 = maxY;
    rooms.forEach(r => { if (r.bal) { bx0 = Math.min(bx0, r.bal.rc.x0); bx1 = Math.max(bx1, r.bal.rc.x1); bz0 = Math.min(bz0, r.bal.rc.y0); bz1 = Math.max(bz1, r.bal.rc.y1); } });

    const colliders = [...walls, ...rails].map(b => ({
        x: b.pos[0], z: b.pos[2], hx: b.size[0] / 2, hz: b.size[2] / 2,
        y0: b.pos[1] - b.size[1] / 2, y1: b.pos[1] + b.size[1] / 2
    }));

    const living = rooms.find(r => r.name.includes('סלון') && !r.bal) || rooms.find(r => !r.bal) || rooms[0];
    const sx = living ? (living.x0 + living.x1) / 2 : (minX + maxX) / 2;
    const sz = living ? (living.y0 + living.y1) / 2 : (minY + maxY) / 2;
    const ddx = (bx0 + bx1) / 2 - sx, ddz = (bz0 + bz1) / 2 - sz;
    const yaw = Math.abs(ddx) + Math.abs(ddz) < 0.3 ? 0 : Math.atan2(-ddx, -ddz);

    return {
        walls, glass, frames, doors, floors, furn, rails, roof, labels, colliders,
        start: { x: sx, z: sz, yaw },
        cx: (bx0 + bx1) / 2, cz: (bz0 + bz1) / 2, size: Math.max(bx1 - bx0, bz1 - bz0, 4)
    };
}

function sunState(hour, sc) {
    const t = Math.min(1, Math.max(0, (hour - 6) / 14));
    const elev = Math.sin(t * Math.PI);
    const el = ((6 + elev * 64) * Math.PI) / 180;
    const R = sc.size * 2.2;
    const low = (1 - elev) * (1 - elev);
    return {
        pos: [sc.cx + (1 - 2 * t) * R * 0.9 * Math.cos(el), Math.max(2, R * Math.sin(el)), sc.cz + R * 0.45 * Math.cos(el)],
        intensity: 0.35 + 0.9 * elev,
        ambient: 0.45 + 0.35 * elev,
        color: new THREE.Color('#fff6e5').lerp(new THREE.Color('#ff9a4d'), low).getStyle(),
        sky: new THREE.Color('#cfe6ff').lerp(new THREE.Color('#f6b77c'), low).getStyle(),
        R
    };
}

// ===================== רכיבי ציור =====================
const Box = ({ pos, size, color, opacity = 1, cast = true }) => (
    <mesh position={pos} castShadow={cast} receiveShadow>
        <boxGeometry args={size} />
        <meshStandardMaterial color={color} transparent={opacity < 1} opacity={opacity} />
    </mesh>
);

const B = ({ p, s, c, o = 1 }) => <Box pos={p} size={s} color={c} opacity={o} />;

const Cyl = ({ p, r, h, c, rot }) => (
    <mesh position={p} rotation={rot || [0, 0, 0]} castShadow>
        <cylinderGeometry args={[r, r, h, 20]} />
        <meshStandardMaterial color={c} />
    </mesh>
);

function FloorMesh({ pos, size, color, kind }) {
    const tex = useMemo(() => ((kind === 'wood' || kind === 'tile') ? makeFloorTexture(kind, size[0], size[2]) : null), [kind, size[0], size[2]]);
    useEffect(() => () => { if (tex) tex.dispose(); }, [tex]);
    return (
        <mesh position={pos} receiveShadow>
            <boxGeometry args={size} />
            <meshStandardMaterial color={tex ? '#ffffff' : color} map={tex || null} />
        </mesh>
    );
}

function Door({ d, open }) {
    const jamb = '#8b6b4a';
    const j1 = d.horiz ? [d.x - d.w / 2, 1.05, d.z] : [d.x, 1.05, d.z - d.w / 2];
    const j2 = d.horiz ? [d.x + d.w / 2, 1.05, d.z] : [d.x, 1.05, d.z + d.w / 2];
    const js = d.horiz ? [0.05, 2.1, d.t + 0.02] : [d.t + 0.02, 2.1, 0.05];
    const jambs = (<><B p={j1} s={js} c={jamb} /><B p={j2} s={js} c={jamb} /></>);

    if (d.kind === 'balcony') {
        return (<>{jambs}<Box pos={[d.x, 1.05, d.z]} size={d.horiz ? [d.w, 2.1, 0.03] : [0.03, 2.1, d.w]} color="#7dd3fc" opacity={0.4} cast={false} /></>);
    }

    const hinge = d.horiz ? [d.x - d.w / 2, 0, d.z] : [d.x, 0, d.z - d.w / 2];
    const toPos = d.horiz ? d.roomC.z > d.z : d.roomC.x > d.x;
    const sign = d.horiz ? (toPos ? -1 : 1) : (toPos ? 1 : -1);
    const ang = open ? sign * 1.3 : 0;
    const col = d.kind === 'entrance' ? '#6b3f1d' : '#d9c3a0';
    return (
        <>
            {jambs}
            <group position={hinge} rotation={[0, ang, 0]}>
                <B p={d.horiz ? [d.w / 2, 1.025, 0] : [0, 1.025, d.w / 2]} s={d.horiz ? [d.w - 0.06, 2.05, 0.04] : [0.04, 2.05, d.w - 0.06]} c={col} />
                <B p={d.horiz ? [d.w - 0.1, 1.0, 0.03] : [0.03, 1.0, d.w - 0.1]} s={[0.03, 0.03, 0.03]} c="#c0c0c0" />
            </group>
        </>
    );
}

function Shape({ kind, W, D, h }) {
    switch (kind) {
        case 'sofa':
            return (<>
                <B p={[0, 0.2, 0]} s={[W, 0.4, D]} c="#5c7a99" />
                <B p={[0, 0.62, -D / 2 + 0.11]} s={[W, 0.46, 0.22]} c="#4f6b88" />
                <B p={[-W / 2 + 0.1, 0.3, 0]} s={[0.2, 0.6, D]} c="#4f6b88" />
                <B p={[W / 2 - 0.1, 0.3, 0]} s={[0.2, 0.6, D]} c="#4f6b88" />
                <B p={[-W / 4 + 0.02, 0.46, 0.1]} s={[W / 2 - 0.22, 0.12, D - 0.32]} c="#7391b3" />
                <B p={[W / 4 - 0.02, 0.46, 0.1]} s={[W / 2 - 0.22, 0.12, D - 0.32]} c="#7391b3" />
            </>);
        case 'bed': {
            const xs = W > 1.2 ? [-W * 0.22, W * 0.22] : [0];
            return (<>
                <B p={[0, 0.14, 0]} s={[W, 0.28, D]} c="#7b5e57" />
                <B p={[0, 0.38, 0.02]} s={[W - 0.06, 0.2, D - 0.1]} c="#f1ece3" />
                <B p={[0, 0.55, -D / 2 + 0.03]} s={[W, 1.1, 0.06]} c="#6d4c41" />
                {xs.map((x, i) => <B key={i} p={[x, 0.54, -D / 2 + 0.3]} s={[Math.min(0.55, W * 0.4), 0.12, 0.32]} c="#ffffff" />)}
                <B p={[0, 0.495, D * 0.2]} s={[W - 0.08, 0.03, D * 0.5]} c="#9db4c0" />
            </>);
        }
        case 'wardrobe': {
            const n = Math.max(2, Math.round(W / 0.5));
            return (<>
                <B p={[0, 1.1, 0]} s={[W, 2.2, D]} c="#8d6e63" />
                {Array.from({ length: n - 1 }).map((_, i) => (
                    <B key={i} p={[-W / 2 + ((i + 1) * W) / n, 1.1, D / 2 + 0.003]} s={[0.012, 2.1, 0.006]} c="#4e342e" />
                ))}
            </>);
        }
        case 'bookcase':
            return (<>
                <B p={[0, 0.9, 0]} s={[W, 1.8, D]} c="#a98467" />
                {[1, 2, 3, 4].map(i => <B key={i} p={[0, i * 0.36, D / 2 + 0.003]} s={[W - 0.04, 0.02, 0.006]} c="#6d4c41" />)}
            </>);
        case 'fridge':
            return (<>
                <B p={[0, 0.9, 0]} s={[W, 1.8, D]} c="#e5e7eb" />
                <B p={[0, 1.25, D / 2 + 0.003]} s={[W, 0.012, 0.006]} c="#9ca3af" />
                <B p={[W / 2 - 0.1, 1.5, D / 2 + 0.02]} s={[0.03, 0.35, 0.03]} c="#9ca3af" />
                <B p={[W / 2 - 0.1, 0.95, D / 2 + 0.02]} s={[0.03, 0.5, 0.03]} c="#9ca3af" />
            </>);
        case 'stove': {
            const r = Math.min(W, D) * 0.14;
            return (<>
                <B p={[0, 0.45, 0]} s={[W, 0.9, D]} c="#cbd5e1" />
                <B p={[0, 0.906, 0]} s={[W * 0.92, 0.012, D * 0.92]} c="#111827" />
                {[[-1, -1], [1, -1], [-1, 1], [1, 1]].map(([a, b], i) => <Cyl key={i} p={[a * W * 0.22, 0.92, b * D * 0.22]} r={r} h={0.014} c="#374151" />)}
            </>);
        }
        case 'ksink':
            return (<>
                <B p={[0, 0.45, 0]} s={[W, 0.9, D]} c="#e5e7eb" />
                <B p={[0, 0.905, 0]} s={[W * 0.62, 0.02, D * 0.6]} c="#94a3b8" />
                <Cyl p={[0, 1.02, -D * 0.3]} r={0.015} h={0.25} c="#9ca3af" />
            </>);
        case 'kcabinet':
            return (<>
                <B p={[0, 0.45, 0]} s={[W, 0.9, D]} c="#d6bfa0" />
                <B p={[0, 0.915, 0]} s={[W + 0.02, 0.03, D + 0.02]} c="#374151" />
                <B p={[0, 0.45, D / 2 + 0.003]} s={[0.012, 0.8, 0.006]} c="#8d6e63" />
            </>);
        case 'sink':
            return (<>
                <B p={[0, 0.4, 0]} s={[W, 0.8, D]} c="#e2e8f0" />
                <B p={[0, 0.82, 0]} s={[W * 0.85, 0.04, D * 0.85]} c="#ffffff" />
                <Cyl p={[0, 0.95, -D * 0.3]} r={0.015} h={0.2} c="#9ca3af" />
            </>);
        case 'toilet':
            return (<>
                <B p={[0, 0.2, 0.1]} s={[W * 0.8, 0.4, D - 0.2]} c="#ffffff" />
                <B p={[0, 0.415, 0.1]} s={[W * 0.8, 0.03, D - 0.2]} c="#f1f5f9" />
                <B p={[0, 0.6, -D / 2 + 0.1]} s={[W * 0.85, 0.4, 0.2]} c="#ffffff" />
            </>);
        case 'tub':
            return (<>
                <B p={[0, 0.05, 0]} s={[W, 0.1, D]} c="#f8fafc" />
                <B p={[0, 0.275, -D / 2 + 0.03]} s={[W, 0.55, 0.06]} c="#f8fafc" />
                <B p={[0, 0.275, D / 2 - 0.03]} s={[W, 0.55, 0.06]} c="#f8fafc" />
                <B p={[-W / 2 + 0.03, 0.275, 0]} s={[0.06, 0.55, D]} c="#f8fafc" />
                <B p={[W / 2 - 0.03, 0.275, 0]} s={[0.06, 0.55, D]} c="#f8fafc" />
                <B p={[0, 0.4, 0]} s={[W - 0.12, 0.01, D - 0.12]} c="#bfe3f2" />
            </>);
        case 'shower':
            return (<>
                <B p={[0, 0.03, 0]} s={[W, 0.06, D]} c="#e2e8f0" />
                <B p={[0, 1.0, D / 2]} s={[W, 2.0, 0.02]} c="#bae6fd" o={0.3} />
                <B p={[W / 2, 1.0, 0]} s={[0.02, 2.0, D]} c="#bae6fd" o={0.3} />
            </>);
        case 'washer':
            return (<>
                <B p={[0, 0.425, 0]} s={[W, 0.85, D]} c="#f8fafc" />
                <Cyl p={[0, 0.45, D / 2 + 0.005]} r={Math.min(W, D) * 0.3} h={0.02} c="#94a3b8" rot={[Math.PI / 2, 0, 0]} />
            </>);
        case 'table':
            return (<>
                <B p={[0, h - 0.025, 0]} s={[W, 0.05, D]} c="#b08968" />
                {[[-1, -1], [1, -1], [-1, 1], [1, 1]].map(([a, b], i) => (
                    <B key={i} p={[a * (W / 2 - 0.06), (h - 0.05) / 2, b * (D / 2 - 0.06)]} s={[0.06, h - 0.05, 0.06]} c="#8d6e63" />
                ))}
            </>);
        case 'chair':
            return (<>
                <B p={[0, 0.43, 0]} s={[W, 0.05, D]} c="#9c6644" />
                {[[-1, -1], [1, -1], [-1, 1], [1, 1]].map(([a, b], i) => (
                    <B key={i} p={[a * (W / 2 - 0.03), 0.2, b * (D / 2 - 0.03)]} s={[0.04, 0.4, 0.04]} c="#7f5539" />
                ))}
                <B p={[0, 0.67, -D / 2 + 0.02]} s={[W, 0.44, 0.04]} c="#9c6644" />
            </>);
        case 'dresser':
            return (<>
                <B p={[0, h / 2, 0]} s={[W, h, D]} c="#a98467" />
                <B p={[0, h + 0.015, 0]} s={[W + 0.02, 0.03, D + 0.02]} c="#caa883" />
                <B p={[0, h / 2, D / 2 + 0.003]} s={[0.012, h - 0.06, 0.006]} c="#6d4c41" />
            </>);
        default:
            return <B p={[0, h / 2, 0]} s={[W, h, D]} c="#94a3b8" />;
    }
}

function Furniture({ item, onSelect }) {
    return (
        <group
            position={[item.cx, 0, item.cz]}
            rotation={[0, item.yaw, 0]}
            onClick={(e) => { e.stopPropagation(); onSelect(item); }}
        >
            <Shape kind={item.kind} W={item.W} D={item.D} h={item.h} />
        </group>
    );
}

// תווית שם חדר: ציור על קנבס בתוך הסצנה (בלי נגיעה ב-DOM)
function Label({ x, z, text }) {
    const { texture, aspect } = useMemo(() => {
        const c = document.createElement('canvas');
        const g = c.getContext('2d');
        const font = 'bold 44px Segoe UI, Arial, sans-serif';
        g.font = font;
        const w = Math.ceil(g.measureText(text).width) + 44, h = 76;
        c.width = w; c.height = h;
        g.font = font;
        g.fillStyle = 'rgba(255,255,255,0.9)';
        const r = 16;
        g.beginPath();
        g.moveTo(r, 0); g.arcTo(w, 0, w, h, r); g.arcTo(w, h, 0, h, r); g.arcTo(0, h, 0, 0, r); g.arcTo(0, 0, w, 0, r);
        g.closePath(); g.fill();
        g.direction = 'rtl'; g.textAlign = 'center'; g.textBaseline = 'middle';
        g.fillStyle = '#334155';
        g.fillText(text, w / 2, h / 2 + 2);
        const t = new THREE.CanvasTexture(c);
        if (THREE.SRGBColorSpace) t.colorSpace = THREE.SRGBColorSpace;
        return { texture: t, aspect: w / h };
    }, [text]);
    useEffect(() => () => texture.dispose(), [texture]);
    return (
        <sprite position={[x, 0.35, z]} scale={[aspect * 0.32, 0.32, 1]} renderOrder={10}>
            <spriteMaterial map={texture} transparent depthTest={false} />
        </sprite>
    );
}

function Sky({ color }) {
    const { scene } = useThree();
    useEffect(() => { scene.background = new THREE.Color(color); }, [scene, color]);
    return null;
}

// הליכה בגובה עיניים: WASD / חצים, גרירה להסתכלות, התנגשות בקירות
function Walker({ enabled, start, colliders }) {
    const { camera, gl } = useThree();
    const st = useRef({ yaw: 0, pitch: 0, keys: {}, drag: false, lx: 0, ly: 0 });

    useEffect(() => {
        if (!enabled) return undefined;
        const s = st.current;
        s.keys = {};
        s.yaw = start.yaw; s.pitch = 0;
        camera.rotation.order = 'YXZ';
        camera.position.set(start.x, 1.6, start.z);
        const kd = (e) => { s.keys[e.code] = true; };
        const ku = (e) => { s.keys[e.code] = false; };
        const pd = (e) => { s.drag = true; s.lx = e.clientX; s.ly = e.clientY; };
        const pm = (e) => {
            if (!s.drag) return;
            s.yaw -= (e.clientX - s.lx) * 0.004;
            s.pitch = Math.max(-1.2, Math.min(1.2, s.pitch - (e.clientY - s.ly) * 0.004));
            s.lx = e.clientX; s.ly = e.clientY;
        };
        const pu = () => { s.drag = false; };
        const el = gl.domElement;
        window.addEventListener('keydown', kd);
        window.addEventListener('keyup', ku);
        el.addEventListener('pointerdown', pd);
        window.addEventListener('pointermove', pm);
        window.addEventListener('pointerup', pu);
        return () => {
            window.removeEventListener('keydown', kd);
            window.removeEventListener('keyup', ku);
            el.removeEventListener('pointerdown', pd);
            window.removeEventListener('pointermove', pm);
            window.removeEventListener('pointerup', pu);
            s.keys = {};
        };
    }, [enabled]);

    useFrame((_, dt) => {
        if (!enabled) return;
        const s = st.current, k = s.keys;
        camera.rotation.set(s.pitch, s.yaw, 0);
        let f = 0, r = 0;
        if (k.KeyW || k.ArrowUp) f += 1;
        if (k.KeyS || k.ArrowDown) f -= 1;
        if (k.KeyD || k.ArrowRight) r += 1;
        if (k.KeyA || k.ArrowLeft) r -= 1;
        if (!f && !r) return;
        const speed = ((k.ShiftLeft || k.ShiftRight) ? 4 : 2) * Math.min(dt, 0.05);
        const dx = (-Math.sin(s.yaw) * f + Math.cos(s.yaw) * r) * speed;
        const dz = (-Math.cos(s.yaw) * f - Math.sin(s.yaw) * r) * speed;
        const R = 0.22;
        const hit = (nx, nz) => colliders.some(c =>
            Math.abs(nx - c.x) < c.hx + R && Math.abs(nz - c.z) < c.hz + R && c.y1 > 0.3 && c.y0 < 1.7);
        if (!hit(camera.position.x + dx, camera.position.z)) camera.position.x += dx;
        if (!hit(camera.position.x, camera.position.z + dz)) camera.position.z += dz;
    });
    return null;
}

const Model = React.memo(function Model({ sc, showRoof, half, doorsOpen, onSelect, modelRef }) {
    return (
        <group ref={modelRef}>
            {sc.floors.map((f, i) => <FloorMesh key={`f${i}`} {...f} />)}
            {sc.walls.map((b, i) => <Box key={`w${i}`} {...b} />)}
            {sc.frames.map((b, i) => <Box key={`fr${i}`} {...b} />)}
            {sc.glass.map((b, i) => <Box key={`g${i}`} {...b} opacity={0.4} cast={false} />)}
            {!half && sc.doors.map((d, i) => <Door key={`d${i}`} d={d} open={doorsOpen} />)}
            {sc.furn.map((f, i) => <Furniture key={`m${i}`} item={f} onSelect={onSelect} />)}
            {sc.rails.map((b, i) => <Box key={`r${i}`} {...b} cast={false} />)}
            {showRoof && !half && sc.roof.map((b, i) => <Box key={`t${i}`} {...b} />)}
        </group>
    );
});

export default function Plan3D({ layoutData, onClose }) {
    const [showRoof, setShowRoof] = useState(false);
    const [showNames, setShowNames] = useState(true);
    const [half, setHalf] = useState(false);
    const [doorsOpen, setDoorsOpen] = useState(true);
    const [hour, setHour] = useState(12);
    const [walk, setWalk] = useState(false);
    const [selected, setSelected] = useState(null);
    const [viewKey, setViewKey] = useState(0);
    const glRef = useRef(null);
    const modelRef = useRef(null);

    const sc = useMemo(() => buildScene(layoutData, half ? HALF_H : WALL_H), [layoutData, half]);
    const sun = useMemo(() => sunState(hour, sc), [hour, sc]);

    const hh = Math.floor(hour), mm = Math.round((hour - hh) * 60);
    const clock = `${String(hh).padStart(2, '0')}:${String(mm).padStart(2, '0')}`;

    const toggleWalk = () => {
        setSelected(null);
        if (walk) { setWalk(false); setViewKey(k => k + 1); }
        else { setHalf(false); setWalk(true); }
    };

    const shot = () => {
        const gl = glRef.current;
        if (!gl) return;
        const a = document.createElement('a');
        a.href = gl.domElement.toDataURL('image/png');
        a.download = 'apartment-3d.png';
        a.click();
    };

    const exportGlb = async () => {
        try {
            const { GLTFExporter } = await import('three/examples/jsm/exporters/GLTFExporter.js');
            new GLTFExporter().parse(modelRef.current, (result) => {
                const blob = new Blob([result], { type: 'application/octet-stream' });
                const a = document.createElement('a');
                a.href = URL.createObjectURL(blob);
                a.download = 'apartment.glb';
                a.click();
            }, (err) => alert('ייצוא נכשל: ' + err), { binary: true });
        } catch (e) {
            alert('ייצוא נכשל: ' + e.message);
        }
    };

    const btn = (active) => ({
        background: active ? '#0ea5e9' : '#f1f5f9', color: active ? '#fff' : '#475569',
        border: '1px solid ' + (active ? '#0ea5e9' : '#cbd5e1'),
        padding: '7px 12px', borderRadius: '8px', cursor: 'pointer', fontWeight: 600, fontSize: '13px'
    });

    return (
        <div style={{ position: 'fixed', inset: 0, zIndex: 2000, background: '#e8eef5', display: 'flex', flexDirection: 'column', direction: 'rtl', fontFamily: 'Segoe UI, sans-serif' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '12px', flexWrap: 'wrap', padding: '10px 16px', background: '#fff', boxShadow: '0 2px 6px rgba(0,0,0,0.08)' }}>
                <div>
                    <div style={{ fontWeight: 700, fontSize: '18px', color: '#1e293b' }}>🏠 תצוגת תלת-ממד</div>
                    <div style={{ fontSize: '12px', color: '#64748b' }}>גרירה: סיבוב | גלגלת: זום | קליק ימני: הזזה | קליק על רהיט: פרטים</div>
                </div>
                <div style={{ display: 'flex', gap: '6px', flexWrap: 'wrap', alignItems: 'center' }}>
                    <button style={btn(showRoof && !half)} onClick={() => setShowRoof(v => !v)}>{showRoof ? '🔓 הסתר תקרה' : '🔒 הצג תקרה'}</button>
                    <button style={btn(half)} onClick={() => { setHalf(v => !v); setWalk(false); }}>🏠 בית בובות</button>
                    <button style={btn(doorsOpen)} onClick={() => setDoorsOpen(v => !v)}>{doorsOpen ? '🚪 דלתות פתוחות' : '🚪 דלתות סגורות'}</button>
                    <button style={btn(showNames)} onClick={() => setShowNames(v => !v)}>{showNames ? 'הסתר שמות' : 'הצג שמות'}</button>
                    <label style={{ ...btn(false), display: 'flex', alignItems: 'center', gap: '6px', cursor: 'default' }}>
                        ☀️ {clock}
                        <input type="range" min="6" max="20" step="0.25" value={hour} onChange={(e) => setHour(parseFloat(e.target.value))} />
                    </label>
                    <button style={btn(walk)} onClick={toggleWalk}>{walk ? '🧍 יציאה מהליכה' : '🚶 הליכה בדירה'}</button>
                    <button style={btn(false)} onClick={shot}>📷 צילום</button>
                    <button style={btn(false)} onClick={exportGlb}>📦 ייצוא GLB</button>
                    <button style={btn(false)} onClick={() => { setWalk(false); setViewKey(k => k + 1); }}>↺ איפוס</button>
                    <button style={{ ...btn(false), background: '#0f172a', color: '#fff', border: 'none' }} onClick={onClose}>✕ סגירה</button>
                </div>
            </div>

            <div style={{ flex: 1, minHeight: 0, position: 'relative' }}>
                <Canvas
                    key={viewKey}
                    shadows
                    gl={{ preserveDrawingBuffer: true }}
                    camera={{ position: [sc.cx + sc.size * 0.8, sc.size * 0.9, sc.cz + sc.size * 1.1], fov: 45 }}
                    onCreated={({ gl }) => { glRef.current = gl; }}
                >
                    <Sky color={sun.sky} />
                    <ambientLight intensity={sun.ambient} />
                    <hemisphereLight args={['#ffffff', '#b8c4a8', 0.35]} />
                    <directionalLight
                        castShadow
                        position={sun.pos}
                        intensity={sun.intensity}
                        color={sun.color}
                        shadow-mapSize={[2048, 2048]}
                        shadow-camera-near={1}
                        shadow-camera-far={sun.R * 3}
                        shadow-camera-left={-sc.size * 1.4}
                        shadow-camera-right={sc.size * 1.4}
                        shadow-camera-top={sc.size * 1.4}
                        shadow-camera-bottom={-sc.size * 1.4}
                        shadow-bias={-0.0004}
                    />
                    <OrbitControls makeDefault enabled={!walk} target={[sc.cx, 0, sc.cz]} maxPolarAngle={Math.PI / 2.02} />
                    <Walker enabled={walk} start={sc.start} colliders={sc.colliders} />

                    <mesh position={[sc.cx, -0.17, sc.cz]} rotation={[-Math.PI / 2, 0, 0]} receiveShadow>
                        <planeGeometry args={[sc.size * 8, sc.size * 8]} />
                        <meshStandardMaterial color="#cfe0c3" />
                    </mesh>

                    <Model sc={sc} showRoof={showRoof} half={half} doorsOpen={doorsOpen} onSelect={setSelected} modelRef={modelRef} />

                    {showNames && !walk && sc.labels.map((l, i) => <Label key={`l${i}-${l.text}`} x={l.x} z={l.z} text={l.text} />)}
                </Canvas>

                {walk && (
                    <div style={{ position: 'absolute', bottom: 14, right: 14, background: 'rgba(15,23,42,0.8)', color: '#fff', padding: '8px 14px', borderRadius: '10px', fontSize: '13px' }}>
                        W/A/S/D או חצים: הליכה | גרירה בעכבר: הסתכלות | Shift: מהר
                    </div>
                )}

                {selected && !walk && (
                    <div style={{ position: 'absolute', bottom: 14, left: 14, background: '#fff', padding: '10px 14px', borderRadius: '10px', boxShadow: '0 4px 12px rgba(0,0,0,0.15)', fontSize: '14px', color: '#1e293b' }}>
                        <strong>{selected.name}</strong>
                        <div style={{ color: '#64748b', fontSize: '12px' }}>
                            {Math.round(selected.w * 100)} × {Math.round(selected.d * 100)} ס"מ | גובה כ-{Math.round(selected.h * 100)} ס"מ
                        </div>
                        <button onClick={() => setSelected(null)} style={{ marginTop: 6, border: 'none', background: '#f1f5f9', borderRadius: 6, padding: '3px 10px', cursor: 'pointer' }}>סגירה</button>
                    </div>
                )}
            </div>
        </div>
    );
}