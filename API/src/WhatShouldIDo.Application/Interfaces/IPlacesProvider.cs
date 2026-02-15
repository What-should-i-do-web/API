using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface IPlacesProvider
    {
        // Kısa keyword'e göre yakın mekanlar (Nearby Search API)
        Task<List<Place>> GetNearbyPlacesAsync(float lat, float lng, int radius, string keyword = null);

        // Serbest metin (prompt) araması → Text Search API
        Task<List<Place>> SearchByPromptAsync(string textQuery, float lat, float lng, string[] priceLevels = null);

        // AI-powered search with detailed results
        Task<List<PlaceDto>> SearchNearbyAsync(double lat, double lng, int radius, string? types, int maxResults);

        // Get detailed information about a specific place
        Task<PlaceDto?> GetPlaceDetailsAsync(string placeId);
    }
}
