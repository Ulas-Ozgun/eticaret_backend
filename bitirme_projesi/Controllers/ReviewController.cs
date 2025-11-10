using Microsoft.AspNetCore.Mvc;
using bitirme_projesi.Data;
using bitirme_projesi.Models;

namespace bitirme_projesi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReviewController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public IActionResult AddReview([FromBody] Review review)
        {
            // Debug amaçlı gelen veriyi yazdıralım
            Console.WriteLine($"Gelen veri: UserId={review.UserId}, ProductId={review.ProductId}, Comment={review.Comment}, Rating={review.Rating}");

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = "Model hatası", errors });
            }

            review.CreatedAt = DateTime.UtcNow;
            _context.Reviews.Add(review);
            _context.SaveChanges();

            return Ok(new { message = "Yorum başarıyla eklendi!" });
        }

        [HttpGet("{productId}")]
        public IActionResult GetReviews(int productId)
        {
            var reviews = _context.Reviews
                .Where(r => r.ProductId == productId)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            return Ok(reviews);
        }
        [HttpDelete("{id}")]
        public IActionResult DeleteReview(int id, [FromQuery] int userId)
        {
            var review = _context.Reviews.FirstOrDefault(r => r.Id == id);
            if (review == null)
                return NotFound(new { message = "Yorum bulunamadı." });

            if (review.UserId != userId)
                return Forbid("Bu yorumu silme yetkiniz yok.");

            _context.Reviews.Remove(review);
            _context.SaveChanges();

            return Ok(new { message = "Yorum başarıyla silindi!" });
        }

    }
}