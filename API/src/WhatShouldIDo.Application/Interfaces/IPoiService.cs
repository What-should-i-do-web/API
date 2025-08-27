using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface IPoiService
    {
        Task<PoiDto> CreateAsync(CreatePoiRequest request);
        Task<IEnumerable<PoiDto>> GetAllAsync();
        Task<PoiDto> GetByIdAsync(Guid id);
        Task<PoiDto> UpdateAsync(Guid id, UpdatePoiRequest request);
        Task DeleteAsync(Guid id);
        Task<IEnumerable<PoiDto>> GetNearbyAsync(float lat, float lng, int radiusMeters, string[]? types = null, int maxResults = 20);
    }
}
