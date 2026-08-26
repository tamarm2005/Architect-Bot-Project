import React, { createContext, useState } from 'react';

export const PlotContext = createContext();

export const PlotProvider = ({ children }) => {
    const [plotOptions, setPlotOptions] = useState([]); // כל המגרשים שזוהו
    const [selectedPlot, setSelectedPlot] = useState(null); // המגרש שנבחר סופית
    const [programData, setProgramData] = useState({
        rooms: [],
        budget: 500,
        tech: [],
        preferences: { style: '', hosting: 'low' }
    });
    return (
        <PlotContext.Provider value={{ plotOptions, setPlotOptions, selectedPlot, setSelectedPlot, programData, setProgramData }}>
            {children}
        </PlotContext.Provider>
    );
};

