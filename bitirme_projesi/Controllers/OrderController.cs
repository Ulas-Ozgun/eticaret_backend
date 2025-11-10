using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using bitirme_projesi.Data;
using bitirme_projesi.Models;

namespace bitirme_projesi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrderController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OrderController(AppDbContext context)
        {
            _context = context;
        }

        // 🔹 1️⃣ Kullanıcının siparişlerini getir
        [HttpGet("user/{userId}")]
        public IActionResult GetUserOrders(int userId)
        {
            var orders = _context.Orders
                .Include(o => o.Product)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.Size,
                    o.Quantity,
                    o.TotalPrice,
                    o.OrderDate,
                    Product = new
                    {
                        o.Product.Name,
                        o.Product.ImageUrl,
                        o.Product.Price
                    }
                })
                .ToList();

            if (!orders.Any())
                return NotFound(new { message = "Henüz bir siparişiniz yok." });

            return Ok(orders);
        }

        // 🔹 2️⃣ Tüm siparişleri getir (Admin)
        [HttpGet("all")]
        public IActionResult GetAllOrders()
        {
            var orders = _context.Orders
                .Include(o => o.Product)
                .Include(o => o.User)
                .Select(o => new
                {
                    o.Id,
                    UserName = o.User.Name,
                    ProductName = o.Product.Name,
                    o.Quantity,
                    o.TotalPrice,
                    o.OrderDate,
                    o.Status
                })
                .ToList();

            return Ok(orders);
        }

        // 🔹 3️⃣ Sipariş durumu güncelle (PUT)
        [HttpPut("{id}")]
        public IActionResult UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status))
                return BadRequest(new { message = "Geçerli bir durum bilgisi gönderilmedi." });

            var order = _context.Orders.FirstOrDefault(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "Sipariş bulunamadı." });

            order.Status = dto.Status;
            _context.SaveChanges();

            return Ok(new { message = "Sipariş durumu güncellendi.", order });
        }
    }
}
