using Microsoft.AspNetCore.Mvc;
using bitirme_projesi.Data;
using bitirme_projesi.Models;
using Microsoft.EntityFrameworkCore;

namespace bitirme_projesi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FavoriteController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FavoriteController(AppDbContext context)
        {
            _context = context;
        }

        // 🔹  Favoriye ekle
        [HttpPost]
        public IActionResult AddToFavorites([FromBody] Favorite dto)
        {
            var user = _context.Users.Find(dto.UserId);
            var product = _context.Products.Find(dto.ProductId);

            if (user == null || product == null)
                return BadRequest(new { message = "Kullanıcı veya ürün bulunamadı." });

            var alreadyFav = _context.Favorites
                .FirstOrDefault(f => f.UserId == dto.UserId && f.ProductId == dto.ProductId);

            if (alreadyFav != null)
                return BadRequest(new { message = "Ürün zaten favorilerde ❤️" });

            _context.Favorites.Add(dto);
            _context.SaveChanges();

            return Ok(new { message = "Ürün favorilere eklendi ❤️" });
        }

        // 🔹  Kullanıcının favorilerini getir
        [HttpGet("get/{userId}")]
        public IActionResult GetFavorites(int userId)
        {
            var favorites = _context.Favorites
                .Include(f => f.Product)
                .Where(f => f.UserId == userId)
                .Select(f => new
                {
                    f.Id,
                    Product = new
                    {
                        f.Product.Id,
                        f.Product.Name,
                        f.Product.Description,
                        f.Product.Price,
                        f.Product.ImageUrl
                    }
                })
                .ToList();

            return Ok(favorites);
        }

        // 🔹 Favoriden kaldır
        [HttpDelete("{id}")]
        public IActionResult RemoveFavorite(int id)
        {
            var fav = _context.Favorites.FirstOrDefault(f => f.Id == id);
            if (fav == null) return NotFound("Favori bulunamadı");

            _context.Favorites.Remove(fav);
            _context.SaveChanges();

            return Ok(new { message = "Favoriden kaldırıldı ❌" });
        }
    }
}
