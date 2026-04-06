using Microsoft.Extensions.Configuration;
using Mscc.GenerativeAI;
using System;

namespace bitirme_projesi.Services
{
    public class GeminiService
    {
        private readonly string _apiKey;
        private static readonly string[] CandidateModels =
        {
            // Önce doğrudan 2.5 Flash; başarısız olursa yedekler
            "gemini-2.5-flash",
            "gemini-flash-latest",
            "gemini-2.0-flash",
            "gemini-2.0-pro",
        };

        public GeminiService(IConfiguration configuration)
        {
            _apiKey = configuration["Gemini:ApiKey"] ?? throw new InvalidOperationException("Gemini:ApiKey configuration is missing.");
        }

        public async Task<string> SummarizeReviews(List<string> reviews)
        {
            if (reviews == null || reviews.Count == 0)
                return string.Empty;

            var prompt =
                "Aşağıdaki 20 adet ürün yorumunu analiz et. Ürünün artılarını, eksilerini ve genel kullanıcı memnuniyetini 3 kısa cümlede Türkçe özetle.\n\n" +
                string.Join("\n", reviews.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => $"- {r.Trim()}"));

            var gemini = new GoogleAI(_apiKey);
            Exception? lastEx = null;

            foreach (var modelName in CandidateModels)
            {
                try
                {
                    var model = gemini.GenerativeModel(modelName);

                    Console.WriteLine("GeminiService: GenerateContent started. model={0} reviewsCount={1}", modelName, reviews.Count);

                    var generateTask = model.GenerateContent(prompt);
                    var timeoutTask = Task.Delay(TimeSpan.FromSeconds(45));

                    var completed = await Task.WhenAny(generateTask, timeoutTask);
                    if (completed == timeoutTask)
                        throw new TimeoutException($"Gemini GenerateContent timeout (45s). model={modelName}");

                    var response = await generateTask;
                    Console.WriteLine("GeminiService: GenerateContent finished. model={0}", modelName);

                    var text = (response?.Text ?? string.Empty).Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Console.WriteLine("GeminiService: model failed. model={0} error={1}", modelName, ex.Message);
                }
            }

            // Hiçbir model çalışmazsa son hatayı fırlat.
            throw new InvalidOperationException("Gemini özet üretimi tüm model denemelerinde başarısız oldu.", lastEx);
        }
    }
}

