using Microsoft.AspNetCore.Http;

namespace bitirme_projesi.Models
{
    public class ImageSearchRequest
    {
        public IFormFile Image { get; set; }
    }
}
