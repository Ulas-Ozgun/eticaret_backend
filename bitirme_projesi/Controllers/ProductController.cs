using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using bitirme_projesi.Data;
using bitirme_projesi.Models;
using System.Linq;
using System.IO;

namespace bitirme_projesi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public ProductController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // 🔹 1️⃣ Tüm ürünleri getir (optimize edilmiş) - Sadece onaylanmış ürünler
        [HttpGet]
        public IActionResult GetAllProducts([FromQuery] int? limit = null, [FromQuery] bool? includePending = null)
        {
            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Seller)
                .Where(p => includePending == true || p.IsApproved)  // 🔹 Sadece onaylanmış ürünler (admin paneli için includePending=true)
                .OrderByDescending(p => p.Id);

            // Eğer limit verilmişse uygula
            if (limit.HasValue && limit.Value > 0)
            {
                query = (IOrderedQueryable<Product>)query.Take(limit.Value);
            }

            var products = query.ToList();
            return Ok(products);
        }

        // 🔹 2️⃣ Tek ürün getir
        [HttpGet("{id}")]
        public IActionResult GetProductById(int id)
        {
            var product = _context.Products
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Seller)
                .FirstOrDefault(p => p.Id == id);

            if (product == null)
                return NotFound(new { message = "Ürün bulunamadı." });

            return Ok(product);
        }

        // 🔹 Helper: Kullanıcı bilgisini al (Header'dan veya Query'den)
        private (int? userId, string? role) GetUserInfo()
        {
            // Header'dan userId ve role al
            var userIdHeader = Request.Headers["X-User-Id"].FirstOrDefault();
            var roleHeader = Request.Headers["X-User-Role"].FirstOrDefault();
            
            if (int.TryParse(userIdHeader, out var userId))
            {
                return (userId, roleHeader);
            }
            
            // Query parameter'dan dene
            var userIdQuery = Request.Query["userId"].FirstOrDefault();
            if (int.TryParse(userIdQuery, out userId))
            {
                return (userId, roleHeader);
            }
            
            return (null, null);
        }

        // 🔹 3️⃣ Yeni ürün ekle (FormData ile)
        [HttpPost("add-with-image")]
        public async Task<IActionResult> AddProductWithImage([FromForm] ProductCreateDto model, [FromQuery] int? userId = null)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // Kullanıcı bilgisini al
            var (currentUserId, userRole) = GetUserInfo();
            var sellerId = userId ?? currentUserId;

            // Satıcı veya Admin kontrolü
            bool isAdmin = false;
            if (sellerId.HasValue)
            {
                var user = await _context.Users.FindAsync(sellerId.Value);
                if (user == null)
                    return BadRequest(new { message = "Kullanıcı bulunamadı." });
                
                if (user.Role != "Admin" && user.Role != "Satıcı")
                    return Forbid("Sadece Admin veya Satıcı ürün ekleyebilir.");
                
                isAdmin = user.Role == "Admin";
            }

            var product = new Product
            {
                Name = model.Name,
                Description = model.Description,
                Price = model.Price,
                Stock = model.Stock,
                CategoryId = model.CategoryId,
                SubCategoryId = model.SubCategoryId,
                Status = model.Stock > 0 ? "Stokta var" : "Tükendi",
                SellerId = sellerId,
                IsApproved = isAdmin  // 🔹 Admin eklediyse true, Satıcı eklediyse false (onay bekliyor)
            };

            // 🔹 Resim yükleme
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "images");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + Path.GetExtension(model.ImageFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ImageFile.CopyToAsync(stream);
                }

                product.ImageUrl = $"images/{uniqueFileName}";
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            // 🔹 Beden ekleme
            if (model.BedenIds != null && model.BedenIds.Any())
            {
                foreach (var bedenId in model.BedenIds)
                {
                    _context.urun_beden.Add(new UrunBeden
                    {
                        ProductId = product.Id,
                        BedenId = bedenId
                    });
                }
            }

            // 🔹 Numara ekleme
            if (model.NumaraIds != null && model.NumaraIds.Any())
            {
                foreach (var numaraId in model.NumaraIds)
                {
                    _context.urun_numara.Add(new UrunNumara
                    {
                        ProductId = product.Id,
                        NumaraId = numaraId
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "✅ Ürün başarıyla eklendi!", product });
        }

        // 🔹 4️⃣ Ürün güncelle (FormData ile)
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromForm] ProductUpdateDto model, [FromQuery] int? userId = null)
        {
            var product = await _context.Products.Include(p => p.Seller).FirstOrDefaultAsync(p => p.Id == id);
            if (product == null) return NotFound(new { message = "Ürün bulunamadı." });

            // Yetkilendirme kontrolü
            var (currentUserId, userRole) = GetUserInfo();
            var requestUserId = userId ?? currentUserId;

            if (requestUserId.HasValue)
            {
                var user = await _context.Users.FindAsync(requestUserId.Value);
                if (user == null)
                    return BadRequest(new { message = "Kullanıcı bulunamadı." });

                // Admin her ürünü düzenleyebilir, Satıcı sadece kendi ürünlerini
                if (user.Role == "Satıcı" && product.SellerId != requestUserId.Value)
                    return Forbid("Sadece kendi ürünlerinizi düzenleyebilirsiniz.");
                
                if (user.Role != "Admin" && user.Role != "Satıcı")
                    return Forbid("Sadece Admin veya Satıcı ürün düzenleyebilir.");
            }

            // Alanlar geldiyse güncelle
            if (model.Name != null) product.Name = model.Name;
            if (model.Description != null) product.Description = model.Description;
            if (model.Price.HasValue) product.Price = model.Price.Value;
            if (model.Stock.HasValue) product.Stock = model.Stock.Value;
            if (model.CategoryId.HasValue) product.CategoryId = model.CategoryId.Value;

            product.SubCategoryId = model.SubCategoryId ?? product.SubCategoryId;  // ⭐ EKLENDİ

            product.Status = product.Stock > 0 ? "Stokta var" : "Tükendi";

            // Yeni resim geldiyse değiştir
            if (model.ImageFile != null && model.ImageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "images");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid() + Path.GetExtension(model.ImageFile.FileName);
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await model.ImageFile.CopyToAsync(stream);

                product.ImageUrl = $"images/{uniqueFileName}";
            }

            // Beden güncelle
            if (model.BedenIds != null)
            {
                var eskiBedenler = _context.urun_beden.Where(x => x.ProductId == id);
                _context.urun_beden.RemoveRange(eskiBedenler);

                foreach (var bedenId in model.BedenIds)
                    _context.urun_beden.Add(new UrunBeden { ProductId = id, BedenId = bedenId });
            }

            // Numara güncelle
            if (model.NumaraIds != null)
            {
                var eskiNumaralar = _context.urun_numara.Where(x => x.ProductId == id);
                _context.urun_numara.RemoveRange(eskiNumaralar);

                foreach (var numaraId in model.NumaraIds)
                    _context.urun_numara.Add(new UrunNumara { ProductId = id, NumaraId = numaraId });
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "✏️ Ürün başarıyla güncellendi!", product });
        }

        // 🔹 5️⃣ Ürün sil
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id, [FromQuery] int? userId = null)
        {
            var product = await _context.Products.Include(p => p.Seller).FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
                return NotFound(new { message = "Ürün bulunamadı." });

            // Yetkilendirme kontrolü
            var (currentUserId, userRole) = GetUserInfo();
            var requestUserId = userId ?? currentUserId;

            if (requestUserId.HasValue)
            {
                var user = await _context.Users.FindAsync(requestUserId.Value);
                if (user == null)
                    return BadRequest(new { message = "Kullanıcı bulunamadı." });

                // Admin her ürünü silebilir, Satıcı sadece kendi ürünlerini
                if (user.Role == "Satıcı" && product.SellerId != requestUserId.Value)
                    return Forbid("Sadece kendi ürünlerinizi silebilirsiniz.");
                
                if (user.Role != "Admin" && user.Role != "Satıcı")
                    return Forbid("Sadece Admin veya Satıcı ürün silebilir.");
            }

            _context.Products.Remove(product);

            var urunBedenler = _context.urun_beden.Where(x => x.ProductId == id);
            var urunNumaralar = _context.urun_numara.Where(x => x.ProductId == id);

            _context.urun_beden.RemoveRange(urunBedenler);
            _context.urun_numara.RemoveRange(urunNumaralar);

            await _context.SaveChangesAsync();

            return Ok(new { message = "🗑️ Ürün başarıyla silindi!" });
        }

        // 🔹 6️⃣ Satıcının kendi ürünlerini getir
        [HttpGet("seller/{sellerId}")]
        public IActionResult GetSellerProducts(int sellerId)
        {
            var products = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Seller)
                .Where(p => p.SellerId == sellerId)
                .OrderByDescending(p => p.Id)
                .ToList();

            return Ok(products);
        }

        // 🔹 7️⃣ Bekleyen ürünleri getir (Admin için)
        [HttpGet("pending")]
        public IActionResult GetPendingProducts()
        {
            var products = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.SubCategory)
                .Include(p => p.Seller)
                .Where(p => !p.IsApproved && p.SellerId != null)  // Onay bekleyen satıcı ürünleri
                .OrderByDescending(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    p.Stock,
                    p.Status,
                    p.ImageUrl,
                    CategoryName = p.Category != null ? p.Category.Name : "",
                    SubCategoryName = p.SubCategory != null ? p.SubCategory.Name : "",
                    SellerName = p.Seller != null ? p.Seller.Name : "",
                    SellerId = p.SellerId
                })
                .ToList();

            return Ok(products);
        }

        // 🔹 8️⃣ Ürünü onayla (Admin)
        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveProduct(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return NotFound(new { message = "Ürün bulunamadı." });

            product.IsApproved = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "✅ Ürün onaylandı ve yayınlandı!", product });
        }

        // 🔹 9️⃣ Ürünü reddet (Admin)
        [HttpPost("{id}/reject")]
        public async Task<IActionResult> RejectProduct(int id, [FromQuery] string? reason = null)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
                return NotFound(new { message = "Ürün bulunamadı." });

            // Ürünü sil (veya IsApproved=false bırak, ama biz silelim)
            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            return Ok(new { message = "❌ Ürün reddedildi ve silindi.", reason });
        }

        // 🔹 🔟 Tüm mevcut ürünleri onayla (Admin - Tek seferlik işlem)
        [HttpPost("approve-all")]
        public async Task<IActionResult> ApproveAllProducts()
        {
            var products = await _context.Products.Where(p => !p.IsApproved).ToListAsync();
            var count = products.Count;

            foreach (var product in products)
            {
                product.IsApproved = true;
            }

            await _context.SaveChangesAsync();

            return Ok(new { 
                message = $"✅ {count} ürün onaylandı ve yayınlandı!", 
                approvedCount = count 
            });
        }

        // 🔹 Slider görselleri
        [HttpGet("slider")]
        public IActionResult GetSliderImages()
        {
            var folder = Path.Combine(_env.WebRootPath, "slider-images");

            if (!Directory.Exists(folder))
                return Ok(new List<string>());

            var files = Directory.GetFiles(folder)
                .Select(f => "slider-images/" + Path.GetFileName(f))
                .ToList();

            return Ok(files);
        }
    }
}
