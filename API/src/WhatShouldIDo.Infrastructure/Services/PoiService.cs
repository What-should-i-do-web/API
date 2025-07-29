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

        public PoiService(IPoiRepository poiRepository)
        {
            _poiRepository = poiRepository;
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
    }
}
