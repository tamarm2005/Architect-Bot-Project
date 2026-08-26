using Models;
using System.Collections.Generic;

namespace DAL
{
    public class CadRepository
    {
        private readonly ProjectContext _context;

        public CadRepository(ProjectContext context)
        {
            _context = context;
        }

        public void SaveWalls(List<Wall> walls)
        {
            _context.Walls.AddRange(walls);
            _context.SaveChanges();
        }
    }
}