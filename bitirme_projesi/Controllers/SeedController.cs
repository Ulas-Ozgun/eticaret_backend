using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using bitirme_projesi.Data;
using bitirme_projesi.Models;
using System.Text.Json;
using System.IO;

namespace bitirme_projesi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SeedController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public SeedController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        /// <summary>
        /// 🔥 Fashion Dataset'ten ürünleri ekler (styles.csv + images.csv birleştirilir)
        /// GET: api/Seed/from-fashion-dataset?datasetPath=C:\Users\ulaso\Desktop\fashion-dataset&countPerCategory=200
        /// </summary>
        [HttpGet("from-fashion-dataset")]
        public async Task<IActionResult> SeedFromFashionDataset(
            [FromQuery] string datasetPath = @"C:\Users\ulaso\Desktop\fashion-dataset",
            [FromQuery] int countPerCategory = 200)
        {
            try
            {
                var categories = await _context.Categories.ToListAsync();
                if (!categories.Any())
                    return BadRequest("Kategori bulunamadı!");

                // Dataset klasörünü bul
                string datasetFullPath = null;
                var possiblePaths = new[]
                {
                    datasetPath, // Tam yol
                    Path.Combine(_env.ContentRootPath, datasetPath),
                    Path.Combine(_env.WebRootPath, datasetPath),
                    Path.Combine(Directory.GetCurrentDirectory(), datasetPath)
                };

                datasetFullPath = possiblePaths.FirstOrDefault(p => Directory.Exists(p) || System.IO.File.Exists(p));
                
                if (string.IsNullOrEmpty(datasetFullPath) || !Directory.Exists(datasetFullPath))
                {
                    return BadRequest(new
                    {
                        error = "Dataset klasörü bulunamadı!",
                        message = $"Şu konumlarda aradım: {string.Join(", ", possiblePaths)}",
                        tip = "Dataset klasörünün tam yolunu verin: C:\\Users\\ulaso\\Desktop\\fashion-dataset"
                    });
                }

                // styles.csv ve images.csv dosyalarını bul
                var stylesPath = Path.Combine(datasetFullPath, "styles.csv");
                var imagesPath = Path.Combine(datasetFullPath, "images.csv");

                if (!System.IO.File.Exists(stylesPath))
                {
                    return BadRequest(new { error = "styles.csv bulunamadı!", path = stylesPath });
                }

                if (!System.IO.File.Exists(imagesPath))
                {
                    return BadRequest(new { error = "images.csv bulunamadı!", path = imagesPath });
                }

                // 🔥 1️⃣ styles.csv'yi oku
                var stylesData = ParseCsvFile(stylesPath);
                if (!stylesData.Any())
                {
                    return BadRequest(new { error = "styles.csv boş veya parse edilemedi!" });
                }

                // 🔥 2️⃣ images.csv'yi oku ve ID -> URL mapping oluştur
                var imagesData = ParseCsvFile(imagesPath);
                var imageUrlMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var img in imagesData)
                {
                    var filename = GetValue(img, "filename", "file", "file_name");
                    var link = GetValue(img, "link", "url", "image_url", "imageUrl");
                    
                    if (!string.IsNullOrEmpty(filename) && !string.IsNullOrEmpty(link))
                    {
                        // filename formatı: "15970.jpg" -> ID: "15970"
                        var id = Path.GetFileNameWithoutExtension(filename);
                        if (!string.IsNullOrEmpty(id))
                        {
                            imageUrlMap[id] = link;
                        }
                    }
                }

                var productsToAdd = new List<Product>();
                var random = new Random();
                var categoryStats = new Dictionary<string, int>();

                // 🔥 3️⃣ Kategori eşleştirme (masterCategory -> veritabanı kategorileri)
                var categoryMapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Apparel", "Giyim" },
                    { "Accessories", "Çanta" },
                    { "Footwear", "Ayakkabı" },
                    { "Personal Care", "Kozmetik" },
                    { "Beauty", "Kozmetik" },
                };

                // 🔥 4️⃣ Her veritabanı kategorisi için ürünleri filtrele ve ekle
                foreach (var category in categories)
                {
                    var catName = category.Name ?? "";
                    var itemsForCategory = new List<Dictionary<string, string>>();

                    // styles.csv'deki ürünleri filtrele
                    foreach (var style in stylesData)
                    {
                        var masterCategory = GetValue(style, "masterCategory", "master_category", "category");
                        if (string.IsNullOrEmpty(masterCategory))
                            continue;

                        // Kategori eşleştirmesi yap
                        var mappedCategory = categoryMapping.ContainsKey(masterCategory)
                            ? categoryMapping[masterCategory]
                            : masterCategory;

                        // Eğer eşleşiyorsa ekle
                        if (string.Equals(mappedCategory, catName, StringComparison.OrdinalIgnoreCase))
                        {
                            itemsForCategory.Add(style);
                        }
                    }

                    // Kategoriye uygun ürün sayısını sınırla
                    var itemsToUse = itemsForCategory
                        .OrderBy(x => random.Next())
                        .Take(countPerCategory)
                        .ToList();

                    foreach (var item in itemsToUse)
                    {
                        // ID'yi al
                        var productId = GetValue(item, "id", "ID", "Id");
                        if (string.IsNullOrEmpty(productId))
                            continue;

                        // Resim URL'sini bul
                        if (!imageUrlMap.ContainsKey(productId))
                            continue; // Resim yoksa ürünü atla

                        var imageUrl = imageUrlMap[productId];

                        // Ürün bilgilerini al
                        var productName = GetValue(item, "productDisplayName", "product_display_name", "name", "title", "product");
                        var articleType = GetValue(item, "articleType", "article_type", "type");
                        var baseColour = GetValue(item, "baseColour", "base_colour", "color", "colour");
                        
                        if (string.IsNullOrEmpty(productName))
                        {
                            productName = $"{baseColour} {articleType}";
                            if (string.IsNullOrEmpty(productName))
                                productName = $"{catName} Ürün {productId}";
                        }

                        // Açıklama oluştur
                        var season = GetValue(item, "season", "Season");
                        var usage = GetValue(item, "usage", "Usage");
                        var gender = GetValue(item, "gender", "Gender");

                        var description = $"{productName}. ";
                        if (!string.IsNullOrEmpty(gender))
                            description += $"{gender} için. ";
                        if (!string.IsNullOrEmpty(baseColour))
                            description += $"{baseColour} renk. ";
                        if (!string.IsNullOrEmpty(season))
                            description += $"{season} sezonu. ";
                        if (!string.IsNullOrEmpty(usage))
                            description += $"{usage} kullanım. ";
                        description += "Kaliteli ve şık tasarım.";

                        // Fiyat (dataset'te yok, kategoriye göre rastgele)
                        decimal price;
                        var lowerCatName = catName.ToLower();
                        if (lowerCatName == "giyim")
                            price = Math.Round((decimal)(random.NextDouble() * 300 + 20), 2);
                        else if (lowerCatName == "ayakkabı")
                            price = Math.Round((decimal)(random.NextDouble() * 500 + 50), 2);
                        else if (lowerCatName == "kozmetik")
                            price = Math.Round((decimal)(random.NextDouble() * 200 + 10), 2);
                        else if (lowerCatName == "çanta")
                            price = Math.Round((decimal)(random.NextDouble() * 400 + 30), 2);
                        else if (lowerCatName == "elektronik" || lowerCatName == "saat")
                            price = Math.Round((decimal)(random.NextDouble() * 2000 + 50), 2);
                        else
                            price = Math.Round((decimal)(random.NextDouble() * 200 + 10), 2);

                        // Stok (rastgele)
                        var stock = random.Next(5, 300);

                        var product = new Product
                        {
                            Name = productName,
                            Description = description,
                            Price = price,
                            ImageUrl = imageUrl,
                            CategoryId = category.Id,
                            SubCategoryId = null,
                            Stock = stock,
                            Status = stock > 0 ? "Stokta var" : "Tükendi",
                            IsApproved = true // Dataset'ten eklenen ürünler otomatik onaylı
                        };

                        productsToAdd.Add(product);
                    }

                    if (itemsToUse.Any())
                    {
                        categoryStats[catName] = itemsToUse.Count;
                    }
                }

                if (productsToAdd.Any())
                {
                    await _context.Products.AddRangeAsync(productsToAdd);
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    message = $"✅ Fashion Dataset'ten {productsToAdd.Count} ürün eklendi!",
                    perCategory = categoryStats,
                    totalProducts = await _context.Products.CountAsync(),
                    datasetPath = datasetFullPath,
                    stylesCount = stylesData.Count,
                    imagesCount = imageUrlMap.Count,
                    imageSource = "Fashion Dataset"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message, stackTrace = ex.StackTrace });
            }
        }

        // 🔹 CSV dosyasını parse et
        private List<Dictionary<string, string>> ParseCsvFile(string filePath)
        {
            var items = new List<Dictionary<string, string>>();
            
            try
            {
                var lines = System.IO.File.ReadAllLines(filePath);
                if (lines.Length < 2)
                    return items;

                // İlk satır header (kolon isimleri)
                var headers = ParseCsvLine(lines[0]);
                if (headers.Length == 0)
                    return items;

                // Diğer satırları parse et
                for (int i = 1; i < lines.Length; i++)
                {
                    var values = ParseCsvLine(lines[i]);
                    if (values.Length != headers.Length)
                        continue; // Satır formatı uyumsuzsa atla

                    var item = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    for (int j = 0; j < headers.Length && j < values.Length; j++)
                    {
                        var key = headers[j]?.Trim() ?? "";
                        var value = values[j]?.Trim() ?? "";
                        if (!string.IsNullOrEmpty(key))
                            item[key] = value;
                    }

                    if (item != null)
                        items.Add(item);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CSV parse hatası: {ex.Message}");
            }

            return items;
        }

        // 🔹 CSV satırını parse et (virgül ve tırnak işareti desteği)
        private string[] ParseCsvLine(string line)
        {
            var values = new List<string>();
            var currentValue = new System.Text.StringBuilder();
            var inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        // Çift tırnak (escape)
                        currentValue.Append('"');
                        i++; // Bir sonraki karakteri atla
                    }
                    else
                    {
                        // Tırnak başlangıcı/bitişi
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    // Virgül (değer ayırıcı)
                    values.Add(currentValue.ToString());
                    currentValue.Clear();
                }
                else
                {
                    currentValue.Append(c);
                }
            }

            // Son değeri ekle
            values.Add(currentValue.ToString());

            return values.ToArray();
        }

        // 🔹 Dictionary'den değer al (birden fazla key dene)
        private string GetValue(Dictionary<string, string> dict, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (dict.ContainsKey(key) && !string.IsNullOrWhiteSpace(dict[key]))
                    return dict[key];
            }
            return null;
        }
    }
}
