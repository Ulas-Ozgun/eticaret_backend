using Microsoft.AspNetCore.Mvc;
using bitirme_projesi.Data;
using bitirme_projesi.Models;

namespace bitirme_projesi.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AuthController(AppDbContext context)
        {
            _context = context;
        }

        
        [HttpPost("register")]
        public IActionResult Register([FromBody] User user)
        {
            if (string.IsNullOrEmpty(user.Email) || string.IsNullOrEmpty(user.Password))
                return BadRequest(new { message = "E-posta ve şifre zorunludur." });

            if (_context.Users.Any(u => u.Email == user.Email))
                return BadRequest(new { message = "Bu e-posta zaten kayıtlı." });

            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok(new
            {
                message = "Kayıt başarılı!",
                userId = user.Id,
                name = user.Name,
                email = user.Email
            });
        }

        
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest loginRequest)
        {
            var user = _context.Users.FirstOrDefault(u =>
                u.Email == loginRequest.Email &&
                u.Password == loginRequest.Password);

            if (user == null)
                return Unauthorized("Geçersiz e-posta veya şifre.");

            return Ok(new
            {
                Message = "Giriş başarılı",
                UserId = user.Id,
                Name = user.Name,
                Email = user.Email
            });
        }


    }
}
