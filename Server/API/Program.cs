using BL;
using DAL;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

string cadPath = @"C:\Program Files\Autodesk\AutoCAD 2025";

AssemblyLoadContext.Default.Resolving += (context, assemblyName) =>
{
    string assemblyPath = Path.Combine(cadPath, assemblyName.Name + ".dll");
    if (File.Exists(assemblyPath))
    {
        return context.LoadFromAssemblyPath(assemblyPath);
    }
    return null;
};

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ProjectContext>(options =>
    options.UseSqlServer(@"Server=.\SQLEXPRESS;Database=MyCadProjectDB;Trusted_Connection=True;TrustServerCertificate=True;"));

builder.Services.AddScoped<CadRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// מחקנו את UseHttpsRedirection כדי להקל על הריאקט
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();