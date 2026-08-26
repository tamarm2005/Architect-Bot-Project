import React, { useContext } from 'react';
import { PlotContext } from './PlotContext';
import { useNavigate } from 'react-router-dom';

export default function SelectPlot() {
    const { plotOptions, setSelectedPlot, setProgramData } = useContext(PlotContext);
    const navigate = useNavigate();

    const handlePlotSelection = (option) => {
        setSelectedPlot(option);
        // 🎯 שמירת שטח המגרש וגם את נקודות המגרש עצמן בתוך הסטייט הכללי!
        setProgramData(prev => ({ 
            ...prev, 
            lotArea: option.area,
            plotPoints: option.points 
        }));

        navigate("/plan-program");
    };

    return (
        <div className="container" style={{ padding: '40px', direction: 'rtl' }}>
            <h1>נמצאו מספר אפשרויות למגרש, נא בחר/י את הנכון:</h1>
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(250px, 1fr))', gap: '20px' }}>
                {plotOptions.map((option, index) => (
                    <div key={index} style={{ border: '2px solid #00a884', padding: '15px', borderRadius: '10px', background: '#f9f9f9' }}>
                        <h3>מגרש {index + 1}</h3>
                        <p>שטח: {option.area} מ"ר</p>

                        {(() => {
                            const minX = Math.min(...option.points.map(p => p.x));
                            const minY = Math.min(...option.points.map(p => p.y));
                            const maxX = Math.max(...option.points.map(p => p.x));
                            const maxY = Math.max(...option.points.map(p => p.y));
                            const padding = 20; 
                            const viewBox = `${minX - padding} ${minY - padding} ${(maxX - minX) + 2 * padding} ${(maxY - minY) + 2 * padding}`;

                            return (
                                <svg width="200" height="150" viewBox={viewBox}>
                                    <polygon
                                        points={option.points.map(p => `${p.x},${p.y}`).join(' ')}
                                        fill="#e0f7fa"
                                        stroke="#00796b"
                                        strokeWidth="10"
                                    />
                                </svg>
                            );
                        })()}

                        <button
                            onClick={() => handlePlotSelection(option)}
                            style={{ width: '100%', marginTop: '10px' }}
                        >
                            לבחירה לחץ כאן
                        </button>
                    </div>
                ))}
            </div>
        </div>
    );
}
