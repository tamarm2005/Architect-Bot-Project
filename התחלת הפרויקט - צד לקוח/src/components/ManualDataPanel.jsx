import React, { useState, useContext } from 'react';
import { PlotContext } from './PlotContext';

export default function ManualDataPanel() {
    const { setProgramData } = useContext(PlotContext);
    const [rows, setRows] = useState([{ name: '', area: '' }]);

    const addRow = () => setRows([...rows, { name: '', area: '' }]);

    const handleInputChange = (index, field, value) => {
        const newRows = [...rows];
        newRows[index][field] = value;
        setRows(newRows);
    };

    const confirmData = () => {
        const validRooms = rows
            .filter(r => r.name !== '' && r.area !== '')
            .map((r, idx) => ({
                id: idx + 1,
                name: r.name,
                area: Number(r.area)
            }));
        setProgramData(prev => ({ ...prev, rooms: validRooms }));
    };

    return (
        <div style={{ height: '100%', display: 'flex', flexDirection: 'column', background: '#ffffff', padding: '5px' }}>
            <h4 style={{ margin: '0 0 5px 0', color: '#075e54', fontSize: '13px' }}>הוספת חללים</h4>
            <p style={{ margin: '0 0 5px 0', color: '#94a3b8', fontSize: '10px' }}>⚠️ שם החלל בעברית! השטח הוא הערכה יחסית - המידות הסופיות יותאמו לגודל המגרש בפועל</p>

            <div style={{ flex: 1, overflowY: 'auto' }}>
                {rows.map((row, index) => (
                    <div key={index} style={{ display: 'flex', gap: '4px', marginBottom: '4px' }}>
                        <input
                            placeholder="שם חלל"
                            value={row.name}
                            onChange={(e) => handleInputChange(index, 'name', e.target.value)}
                            style={inputStyle}
                        />
                        <input
                            type="number"
                            placeholder="שטח (מ״ר)"
                            value={row.area}
                            min="0"
                            step="1"
                            onChange={(e) => handleInputChange(index, 'area', e.target.value)}
                            style={{ ...inputStyle, width: '30%' }}
                        />
                    </div>
                ))}
            </div>

            <div style={{ marginTop: 'auto', paddingTop: '4px' }}>
                <button onClick={addRow} style={addBtnStyle}>+ הוסף שורה</button>
                <button onClick={confirmData} style={confirmBtnStyle}>✅ אישור ושליחה</button>
            </div>
        </div>
    );
}

const inputStyle = { width: '70%', padding: '3px', border: '1px solid #dcdcdc', borderRadius: '3px', fontSize: '11px' };
const addBtnStyle = { width: '100%', padding: '3px', background: '#f1f1f1', border: 'none', borderRadius: '3px', marginBottom: '3px', cursor: 'pointer', fontSize: '11px' };
const confirmBtnStyle = { width: '100%', padding: '4px', background: '#00a884', color: '#fff', border: 'none', borderRadius: '3px', cursor: 'pointer', fontWeight: 'bold', fontSize: '11px' };