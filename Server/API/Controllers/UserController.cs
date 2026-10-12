using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DAL;
using Models;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ProjectContext _context;
        private readonly IConfiguration _config;

        public UserController(ProjectContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        public class RegisterDto
        {
            public string Username { get; set; } = "";
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
        }

        public class LoginDto
        {
            public string Email { get; set; } = "";
            public string Password { get; set; } = "";
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password) || string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest("יש למלא שם משתמש, אימייל וסיסמה.");

            if (await _context.Users.AnyAsync(u => u.Email == dto.Email))
                return BadRequest("משתמש עם האימייל הזה כבר קיים.");

            // 🌟 הצפנת הסיסמה - לעולם לא נשמרת כטקסט גלוי במסד הנתונים
            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(dto.Password);

            var newUser = new User
            {
                Username = dto.Username,
                Email = dto.Email,
                Password = hashedPassword
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            string token = GenerateJwtToken(newUser);
            return Ok(new { token, user = new { id = newUser.Id, userName = newUser.Username, email = newUser.Email } });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);

            // 🌟 בדיקת הסיסמה מול ה-hash השמור - לא השוואת טקסט ישירה
            if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.Password))
                return Unauthorized("אימייל או סיסמה שגויים.");

            string token = GenerateJwtToken(user);
            return Ok(new { token, user = new { id = user.Id, userName = user.Username, email = user.Email } });
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Email, user.Email)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Issuer"],
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}