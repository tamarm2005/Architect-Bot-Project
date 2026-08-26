using Microsoft.EntityFrameworkCore;
using Models;
namespace DAL
{
    public class ProjectContext : DbContext
    {
        public DbSet<Wall> Walls { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Style> Styles { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Point3D> Point3Ds { get; set; }
        public DbSet<Opening> Openings { get; set; }
        public DbSet<Material> Materials { get; set; }
        public DbSet<Furniture> Furnitures { get; set; }
        public DbSet<CalculationResult> CalculationResults { get; set; }
        public DbSet<Apartment> Apartments { get; set; }
        public DbSet<Plot> Plots { get; set; }
        public DbSet<ChatSession> ChatSessions { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=MyCadProjectDB;Trusted_Connection=True;");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            foreach (var relationship in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.ClientSetNull;
            }
        }

        public ProjectContext(DbContextOptions<ProjectContext> options) : base(options)
        {

        }
    }
}
