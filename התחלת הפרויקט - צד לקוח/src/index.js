import React from "react";
import ReactDOM from "react-dom/client";
import "./index.css";
import App from "./App";
import { BrowserRouter } from 'react-router-dom';
import { PlotProvider } from "./components/PlotContext"; 

const root = ReactDOM.createRoot(document.getElementById("root"));
root.render(
  <React.StrictMode>
    <PlotProvider> 
      <BrowserRouter>
        <App />
      </BrowserRouter>
    </PlotProvider>
  </React.StrictMode>
);
