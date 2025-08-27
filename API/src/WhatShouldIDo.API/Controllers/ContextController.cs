using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ContextController : ControllerBase
    {
        private readonly IContextEngine _contextEngine;
        private readonly IWeatherService _weatherService;
        private readonly ILogger<ContextController> _logger;

        public ContextController(IContextEngine contextEngine, IWeatherService weatherService, ILogger<ContextController> logger)
        {
            _contextEngine = contextEngine;
            _weatherService = weatherService;
            _logger = logger;
        }

        // GET /api/context/insights?lat=41.0082&lng=28.9784
        [HttpGet("insights")]
        public async Task<IActionResult> GetContextualInsights([FromQuery] float lat, [FromQuery] float lng, CancellationToken cancellationToken = default)
        {
            try
            {
                var insights = await _contextEngine.GetContextualInsights(lat, lng, cancellationToken);
                
                var response = new
                {
                    location = new { latitude = lat, longitude = lng },
                    timeContext = insights.TimeContext.ToString(),
                    season = insights.Season.ToString(),
                    locationContext = insights.Location.ToString(),
                    weather = new
                    {
                        condition = insights.Weather.Condition,
                        temperature = insights.Weather.Temperature,
                        description = insights.Weather.Description,
                        goodForOutdoor = insights.Weather.IsGoodForOutdoor
                    },
                    recommendations = new
                    {
                        suggestedCategories = insights.SuggestedCategories,
                        recommendedActivities = insights.RecommendedActivities
                    },
                    timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting contextual insights for {lat},{lng}", lat, lng);
                return StatusCode(500, new { error = "Failed to get contextual insights" });
            }
        }

        // GET /api/context/weather?lat=41.0082&lng=28.9784
        [HttpGet("weather")]
        public async Task<IActionResult> GetWeather([FromQuery] float lat, [FromQuery] float lng, CancellationToken cancellationToken = default)
        {
            try
            {
                var weather = await _weatherService.GetCurrentWeatherAsync(lat, lng, cancellationToken);
                
                var response = new
                {
                    location = new { latitude = lat, longitude = lng },
                    temperature = weather.Temperature,
                    feelsLike = weather.FeelsLike,
                    condition = weather.Condition,
                    description = weather.Description,
                    humidity = weather.Humidity,
                    windSpeed = weather.WindSpeed,
                    visibility = weather.Visibility,
                    airQuality = weather.AirQualityIndex,
                    suitability = new
                    {
                        goodForOutdoor = weather.IsGoodForOutdoor,
                        goodForWalking = weather.IsGoodForWalking
                    },
                    updatedAt = weather.UpdatedAt
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting weather for {lat},{lng}", lat, lng);
                return StatusCode(500, new { error = "Failed to get weather data" });
            }
        }

        // GET /api/context/forecast?lat=41.0082&lng=28.9784&days=5
        [HttpGet("forecast")]
        public async Task<IActionResult> GetForecast([FromQuery] float lat, [FromQuery] float lng, [FromQuery] int days = 5, CancellationToken cancellationToken = default)
        {
            try
            {
                var forecast = await _weatherService.GetForecastAsync(lat, lng, Math.Min(days, 7), cancellationToken);
                
                var response = new
                {
                    location = new { latitude = lat, longitude = lng },
                    days = forecast.Count,
                    forecast = forecast.Select(f => new
                    {
                        date = f.Date.ToString("yyyy-MM-dd"),
                        maxTemp = f.MaxTemp,
                        minTemp = f.MinTemp,
                        condition = f.Condition,
                        description = f.Description,
                        chanceOfRain = f.ChanceOfRain
                    })
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting forecast for {lat},{lng}", lat, lng);
                return StatusCode(500, new { error = "Failed to get weather forecast" });
            }
        }

        // GET /api/context/time-recommendations
        [HttpGet("time-recommendations")]
        public IActionResult GetTimeRecommendations()
        {
            try
            {
                var now = DateTime.Now;
                var hour = now.Hour;
                var dayOfWeek = now.DayOfWeek;

                var timeContext = hour switch
                {
                    >= 6 and < 9 => "EarlyMorning",
                    >= 9 and < 12 => "Morning",
                    >= 12 and < 14 => "Lunch",
                    >= 14 and < 17 => "Afternoon",
                    >= 17 and < 20 => "Evening",
                    >= 20 and < 23 => "Night",
                    _ => "LateNight"
                };

                var recommendations = GetTimeBasedRecommendations(timeContext, dayOfWeek);

                var response = new
                {
                    currentTime = now.ToString("yyyy-MM-dd HH:mm:ss"),
                    timeContext = timeContext,
                    dayOfWeek = dayOfWeek.ToString(),
                    recommendations = recommendations,
                    tips = GetTimeBasedTips(timeContext, dayOfWeek)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting time recommendations");
                return StatusCode(500, new { error = "Failed to get time recommendations" });
            }
        }

        private static object GetTimeBasedRecommendations(string timeContext, DayOfWeek dayOfWeek)
        {
            var categories = new List<string>();
            var activities = new List<string>();

            switch (timeContext)
            {
                case "EarlyMorning":
                case "Morning":
                    categories.AddRange(new[] { "cafe", "bakery", "breakfast", "park", "museum" });
                    activities.AddRange(new[] { "Kahvaltı", "Sabah yürüyüşü", "Müze gezisi" });
                    break;
                case "Lunch":
                    categories.AddRange(new[] { "restaurant", "fast_food", "lunch", "business" });
                    activities.AddRange(new[] { "Öğle yemeği", "İş görüşmesi", "Hızlı yemek" });
                    break;
                case "Afternoon":
                    categories.AddRange(new[] { "museum", "shopping", "cafe", "park", "tourist_attraction" });
                    activities.AddRange(new[] { "Alışveriş", "Kültür turu", "Kafede çalışma" });
                    break;
                case "Evening":
                case "Night":
                    categories.AddRange(new[] { "restaurant", "bar", "entertainment", "theater", "cinema" });
                    activities.AddRange(new[] { "Akşam yemeği", "Sosyal buluşma", "Eğlence" });
                    break;
            }

            // Weekend vs weekday adjustments
            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            {
                activities.AddRange(new[] { "Hafta sonu aktivitesi", "Aile zamanı", "Keşif" });
                categories.Add("leisure");
            }
            else
            {
                activities.AddRange(new[] { "İş molası", "Günlük rutin", "Verimlilik" });
                categories.Add("work_friendly");
            }

            return new
            {
                categories = categories.Distinct().Take(6).ToList(),
                activities = activities.Distinct().Take(5).ToList()
            };
        }

        private static List<string> GetTimeBasedTips(string timeContext, DayOfWeek dayOfWeek)
        {
            var tips = new List<string>();

            switch (timeContext)
            {
                case "EarlyMorning":
                    tips.Add("Sabah erken saatlerde daha az kalabalık mekanları tercih edin");
                    tips.Add("Güneş doğarken fotoğraf çekmeye uygun yerler arayın");
                    break;
                case "Morning":
                    tips.Add("Kahvaltı için yerel lezzetleri deneyebileceğiniz yerler tercih edin");
                    tips.Add("Müzeler ve kültürel mekanlar sabah saatlerinde daha sakin olur");
                    break;
                case "Lunch":
                    tips.Add("İş yemeği için sessiz ve hızlı servis veren mekanları tercih edin");
                    tips.Add("Öğle yemeği saatlerinde rezervasyon yaptırmayı unutmayın");
                    break;
                case "Afternoon":
                    tips.Add("Öğleden sonra alışveriş ve gezinti için ideal zaman");
                    tips.Add("Kafeler çalışma için öğleden sonra daha uygun olur");
                    break;
                case "Evening":
                    tips.Add("Akşam yemeği için atmosferi güzel mekanları tercih edin");
                    tips.Add("Sosyal aktiviteler için akşam saatleri idealdir");
                    break;
                case "Night":
                    tips.Add("Gece hayatı için güvenli bölgelerdeki mekanları tercih edin");
                    tips.Add("Ulaşım seçeneklerini önceden planlayın");
                    break;
            }

            if (dayOfWeek == DayOfWeek.Friday)
            {
                tips.Add("Cuma akşamı kalabalık olabileceği için rezervasyon önemli");
            }

            if (dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday)
            {
                tips.Add("Hafta sonu daha fazla aile dostu aktivite seçeneği bulabilirsiniz");
            }

            return tips.Take(3).ToList();
        }
    }
}