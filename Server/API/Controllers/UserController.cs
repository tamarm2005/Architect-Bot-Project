using Microsoft.AspNetCore.Mvc;
using MyUser = Models.User;

namespace API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            return Ok($"User ID: {id}");
        }

        [HttpPost("UserDetails")]
        public IActionResult PostUsersDetails([FromBody] string name)
        {
            MyUser user = new MyUser(1);
            return Ok(user);
        }

        [HttpPost("NewUser")]
        public IActionResult PostNewUser([FromBody] MyUser user)
        {
            return Ok(true);
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] MyUser user)
        {
            if (user != null && user.Id == 1)
            {
                return Ok(user);
            }
            return Unauthorized("Invalid credentials");
        }
    }
}