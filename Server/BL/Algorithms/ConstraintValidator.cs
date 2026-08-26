using CadApplication.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace BL.Algorithms
{
    public class ConstraintValidator
    {
        public bool IsValid(ArchitecturalRoom room, double targetArea)
        {
            // בדיקה אם השטח תקין (סטייה של 20%)
            return System.Math.Abs(room.Area - targetArea) < (targetArea * 0.2);
        }
    }
}