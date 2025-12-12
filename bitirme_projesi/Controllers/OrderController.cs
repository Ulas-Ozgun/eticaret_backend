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

        // 🔹 2️⃣ Tüm siparişleri getir (Admin) veya Satıcının kendi ürünlerinin siparişleri
        [HttpGet("all")]
        public IActionResult GetAllOrders([FromQuery] int? sellerId = null)
        {
            var query = _context.Orders
                .Include(o => o.Product)
                    .ThenInclude(p => p.Seller)
                .Include(o => o.User)
                .AsQueryable();

            // Eğer sellerId varsa, sadece o satıcının ürünlerinin siparişlerini getir
            if (sellerId.HasValue)
            {
                query = query.Where(o => o.Product != null && o.Product.SellerId == sellerId.Value);
            }

            var orders = query
                .Select(o => new {
                    o.Id,
                    UserName = o.User != null ? o.User.Name : "Bilinmiyor",
                    UserEmail = o.User != null ? o.User.Email : "—",
                    ProductName = o.Product != null ? o.Product.Name : "Silinmiş Ürün",
                    ProductPrice = o.Product != null ? o.Product.Price : 0,
                    ProductId = o.Product != null ? o.Product.Id : 0,
                    SellerId = o.Product != null && o.Product.Seller != null ? o.Product.Seller.Id : (int?)null,
                    o.Quantity,
                    o.TotalPrice,
                    o.Status,
                    o.OrderDate
                })
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            return Ok(orders);
        }


        // 🔹 3️⃣ Sipariş durumu güncelle (PUT)
        [HttpPut("{id}")]
        public IActionResult UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto, [FromQuery] int? sellerId = null)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status))
                return BadRequest(new { message = "Geçerli bir durum bilgisi gönderilmedi." });

            var order = _context.Orders
                .Include(o => o.Product)
                    .ThenInclude(p => p.Seller)
                .FirstOrDefault(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "Sipariş bulunamadı." });

            // Satıcı kontrolü: Sadece kendi ürünlerinin siparişlerini güncelleyebilir
            if (sellerId.HasValue && order.Product != null && order.Product.SellerId != sellerId.Value)
            {
                return Forbid("Sadece kendi ürünlerinizin siparişlerini güncelleyebilirsiniz.");
            }

            order.Status = dto.Status;
            _context.SaveChanges();

            return Ok(new { message = "Sipariş durumu güncellendi.", order });
        }
        // 🔹 4️⃣ Siparişi sil
        [HttpDelete("{id}")]
        public IActionResult DeleteOrder(int id)
        {
            var order = _context.Orders.FirstOrDefault(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "Silinecek sipariş bulunamadı." });

            _context.Orders.Remove(order);
            _context.SaveChanges();

            return Ok(new { message = "Sipariş başarıyla silindi." });
        }

    }
}
