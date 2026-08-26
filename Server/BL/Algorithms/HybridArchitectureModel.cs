using CadApplication.Algorithms;
using Models;
using System;
using System.Collections.Generic;

namespace BL.Algorithms
{
    public class HybridArchitectureModel
    {
        private readonly GreedyLayoutEngine _layoutEngine;
        private readonly FurniturePlacementAlgorithm _placementAlgorithm;

        public HybridArchitectureModel()
        {
            _layoutEngine = new GreedyLayoutEngine();
            _placementAlgorithm = new FurniturePlacementAlgorithm();
        }

        // 🌟 עכשיו מקבל בנפרד את שטח המגרש האמיתי (לחישוב האחוז), נפרד מגיאומטריית מיקום החדרים
        public LayoutResult ExecuteHybridGeneration(List<ArchitecturalRoom> nlpParsedRooms, List<Point3D> buildingFootprintForRooms, double actualPlotArea)
        {
            Console.WriteLine("Hybrid Model: Starting execution...");
            LayoutResult layoutResult = _layoutEngine.GenerateLayoutGreedy(nlpParsedRooms, buildingFootprintForRooms, actualPlotArea);
            return layoutResult;
        }
    }
}