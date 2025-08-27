using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Domain.ValueObjects;
using WhatShouldIDo.Infrastructure.Repositories;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class PoiService : IPoiService
    {
        private readonly IPoiRepository _poiRepository;
        private readonly GooglePlacesProvider _googlePlacesProvider;

        public PoiService(IPoiRepository poiRepository, GooglePlacesProvider googlePlacesProvider)
        {
            _poiRepository = poiRepository;
            _googlePlacesProvider = googlePlacesProvider;
        }

        public async Task<PoiDto> CreateAsync(CreatePoiRequest request)
        {
            var poi = new Poi(request.Name, new Coordinates(request.Latitude, request.Longitude), request.Description);
            await _poiRepository.AddAsync(poi);
            await _poiRepository.SaveChangesAsync();
            return new PoiDto(poi.Id, poi.Name, poi.Location.Latitude, poi.Location.Longitude, poi.Description);
        }

        public async Task<IEnumerable<PoiDto>> GetAllAsync()
        {
            var pois = await _poiRepository.GetAllAsync();
            return pois.Select(p => new PoiDto(p.Id, p.Name, p.Location.Latitude, p.Location.Longitude, p.Description));
        }

        public async Task<PoiDto> GetByIdAsync(Guid id)
        {
            var poi = await _poiRepository.GetByIdAsync(id);
            return new PoiDto(poi.Id, poi.Name, poi.Location.Latitude, poi.Location.Longitude, poi.Description);
        }

        public async Task<PoiDto> UpdateAsync(Guid id, UpdatePoiRequest request)
        {
            var poi = await _poiRepository.GetByIdAsync(id);
            poi.UpdateDescription(request.Description);
            poi.UpdateName(request.Name);
            _poiRepository.Update(poi);
            await _poiRepository.SaveChangesAsync();
            return new PoiDto(poi.Id, poi.Name, poi.Location.Latitude, poi.Location.Longitude, poi.Description);
        }

        public async Task DeleteAsync(Guid id)
        {
            var poi = await _poiRepository.GetByIdAsync(id);
            _poiRepository.Remove(poi);
            await _poiRepository.SaveChangesAsync();
        }

        public async Task<IEnumerable<PoiDto>> GetNearbyAsync(float lat, float lng, int radiusMeters, string[]? types = null, int maxResults = 20)
        {
            // Map frontend types to Google Places API types
            var mappedTypes = MapToGooglePlaceTypes(types);
            // For now, use only the first type since Google Places API has limitations with multiple types
            var keyword = mappedTypes.Length > 0 ? mappedTypes[0] : "tourist_attraction";
            
            // Use Google Places to find nearby points of interest
            var places = await _googlePlacesProvider.GetNearbyPlacesAsync(lat, lng, radiusMeters, keyword);
            
            return places.Take(maxResults).Select(place => new PoiDto(
                Guid.NewGuid(), // Generate temp ID for external places
                place.Name,
                (double)place.Latitude,
                (double)place.Longitude,
                $"{place.Address} - Rating: {place.Rating} ({place.Category})"
            ));
        }

        private string[] MapToGooglePlaceTypes(string[]? types)
        {
            if (types == null || types.Length == 0)
                return new[] { "tourist_attraction" };

            var mappedTypes = new List<string>();
            
            foreach (var type in types)
            {
                switch (type.ToLower())
                {
                    case "park":
                    case "parks":
                    case "garden":
                    case "gardens":
                        mappedTypes.Add("park");
                        break;
                    case "restaurant":
                    case "restaurants":
                        mappedTypes.Add("restaurant");
                        break;
                    case "museum":
                    case "museums":
                        mappedTypes.Add("museum");
                        break;
                    case "historical_place":
                    case "historical":
                    case "viewpoint":
                    case "scenic_point":
                    case "natural_feature":
                    case "nature":
                    default:
                        mappedTypes.Add("tourist_attraction");
                        break;
                }
            }
            
            return mappedTypes.Distinct().ToArray();
        }
    }
}
