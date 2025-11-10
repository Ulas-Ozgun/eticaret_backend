using Microsoft.AspNetCore.Mvc;
using bitirme_projesi.Data;
using bitirme_projesi.Models;
using System.Linq;

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

        // 🔹 Normal kullanıcı kaydı
        [HttpPost("register")]
        public IActionResult Register([FromBody] User user)
        {
            if (string.IsNullOrEmpty(user.Email) || string.IsNullOrEmpty(user.Password))
                return BadRequest(new { message = "E-posta ve şifre zorunludur." });

            if (_context.Users.Any(u => u.Email == user.Email))
                return BadRequest(new { message = "Bu e-posta zaten kayıtlı." });

            // Varsayılan rol: User
            user.Role = "User";

            _context.Users.Add(user);
            _context.SaveChanges();

            return Ok(new
            {
                message = "Kayıt başarılı!",
                userId = user.Id,
                name = user.Name,
                email = user.Email,
                role = user.Role
            });
        }

        // 🔹 Admin kaydı (sadece 1 kere kullanılabilir)
        [HttpPost("register-admin")]
        public IActionResult RegisterAdmin([FromBody] User admin)
        {
            if (string.IsNullOrEmpty(admin.Email) || string.IsNullOrEmpty(admin.Password))
                return BadRequest(new { message = "E-posta ve şifre zorunludur." });

            if (_context.Users.Any(u => u.Email == admin.Email))
                return BadRequest(new { message = "Bu e-posta zaten kayıtlı." });

            admin.Role = "Admin";

            _context.Users.Add(admin);
            _context.SaveChanges();

            return Ok(new
            {
                message = "Admin başarıyla oluşturuldu!",
                adminId = admin.Id,
                name = admin.Name,
                email = admin.Email,
                role = admin.Role
            });
        }

        // 🔹 Login (hem User hem Admin)
        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginRequest loginRequest)
        {
            var user = _context.Users.FirstOrDefault(u =>
                u.Email == loginRequest.Email &&
                u.Password == loginRequest.Password);

            if (user == null)
                return Unauthorized(new { message = "Geçersiz e-posta veya şifre." });

            return Ok(new
            {
                message = "Giriş başarılı!",
                userId = user.Id,
                name = user.Name,
                email = user.Email,
                role = user.Role
            });
        }

        // 🔹 Tüm kullanıcıları listele (sadece admin erişebilir)
        [HttpGet("users")]
        public IActionResult GetAllUsers()
        {
            var users = _context.Users
                .Select(u => new
                {
                    u.Id,
                    u.Name,
                    u.Email,
                    u.Role
                })
                .ToList();

            return Ok(users);
        }
    }
}
