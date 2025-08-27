using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class SuggestionService : ISuggestionService
    {
        private readonly IPlacesProvider _placesProvider;
        private readonly IPromptInterpreter _promptInterpreter;
        private readonly IGeocodingService _geocodingService;

        public SuggestionService(IPlacesProvider placesProvider, IPromptInterpreter promptInterpreter, IGeocodingService geocodingService)
        {
            _placesProvider = placesProvider;
            _promptInterpreter = promptInterpreter;
            _geocodingService = geocodingService;
        }

        public async Task<List<SuggestionDto>> GetNearbySuggestionsAsync(float lat, float lng, int radius)
        {
            var places = await _placesProvider.GetNearbyPlacesAsync(lat, lng, radius);
            var suggestions = places.Select(place => new SuggestionDto
            {
                Id = Guid.NewGuid(),
                PlaceName = place.Name,
                Reason = "Yakın konumdan önerildi",
                Score = place.Rating != null && double.TryParse(place.Rating, out var rating) ? rating : 0,
                Latitude = place.Latitude,
                Longitude = place.Longitude,
                PhotoReference = place.PhotoReference,
                PhotoUrl = place.PhotoUrl,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            return suggestions;
        }

        public async Task<SuggestionDto> GetRandomSuggestionAsync(float lat, float lng, int radius)
        {
            var places = await _placesProvider.GetNearbyPlacesAsync(lat, lng, radius);
            var random = new Random();
            var selected = places.OrderBy(_ => random.Next()).FirstOrDefault();

            if (selected == null)
                return null;

            return new SuggestionDto
            {
                Id = Guid.NewGuid(),
                PlaceName = selected.Name,
                Reason = "Konumdan rastgele seçildi",
                Score = selected.Rating != null && double.TryParse(selected.Rating, out var rating) ? rating : 0,
                Latitude = selected.Latitude,
                Longitude = selected.Longitude,
                PhotoReference = selected.PhotoReference,
                PhotoUrl = selected.PhotoUrl,
                CreatedAt = DateTime.UtcNow
            };
        }

        public async Task<List<SuggestionDto>> GetPromptSuggestionsAsync(PromptRequest request)
        {
            var interpreted = await _promptInterpreter.InterpretAsync(request.Prompt);
            var allSuggestions = new List<SuggestionDto>();

            float lat = request.Latitude ?? 0;
            float lng = request.Longitude ?? 0;

            // Eğer kullanıcı konum metni yazmışsa → koordinatlarını al
            if (!string.IsNullOrWhiteSpace(interpreted.LocationText))
            {
                (lat, lng) = await _geocodingService.GetCoordinatesAsync(interpreted.LocationText);
            }

            var places = await _placesProvider.SearchByPromptAsync(
                interpreted.TextQuery,
                lat,
                lng,
                interpreted.PricePreferences
            );

            var suggestions = places.Select(place => new SuggestionDto
            {
                Id = Guid.NewGuid(),
                PlaceName = place.Name,
                Reason = $"'{interpreted.TextQuery}' aramasından eşleşti",
                Score = place.Rating != null && double.TryParse(place.Rating, out var rating) ? rating : 0,
                IsSponsored = place.IsSponsored,
                SponsoredUntil = place.SponsoredUntil,
                Latitude = place.Latitude,
                Longitude = place.Longitude,
                PhotoReference = place.PhotoReference,
                PhotoUrl = place.PhotoUrl,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            bool hasValidCoords = request.Latitude > 0 && request.Longitude > 0;

            if (hasValidCoords)
            {
                return suggestions
                    .OrderByDescending(x => x.IsSponsored)
                    .ThenByDescending(x => x.Score)
                    .ThenBy(x => CalculateDistance(
                        request.Latitude.Value,
                        request.Longitude.Value,
                        x.Latitude,
                        x.Longitude))
                    .ToList();
            }
            else
            {
                return suggestions
                    .OrderByDescending(x => x.IsSponsored)
                    .ThenByDescending(x => x.Score)
                    .ToList();
            }


        }
        private double CalculateDistance(float lat1, float lon1, float lat2, float lon2)
        {
            double R = 6371e3; // metre
            double φ1 = lat1 * Math.PI / 180;
            double φ2 = lat2 * Math.PI / 180;
            double Δφ = (lat2 - lat1) * Math.PI / 180;
            double Δλ = (lon2 - lon1) * Math.PI / 180;

            double a = Math.Sin(Δφ / 2) * Math.Sin(Δφ / 2) +
                       Math.Cos(φ1) * Math.Cos(φ2) *
                       Math.Sin(Δλ / 2) * Math.Sin(Δλ / 2);
            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            double distance = R * c; // metre
            return distance;
        }

    }
}
