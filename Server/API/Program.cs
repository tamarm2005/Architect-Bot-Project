using BL;
using DAL;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;

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

// ?? הגדרת אימות JWT - כל בקשה עם [Authorize] תדרוש טוקן תקין
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Issuer"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthentication(); // ?? חייב לבוא לפני UseAuthorization - בודק את הטוקן
app.UseAuthorization();
app.MapControllers();

app.Run();