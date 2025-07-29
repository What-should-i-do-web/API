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
    public class RoutePointService : IRoutePointService
    {
        private readonly IRoutePointRepository _routePointRepository;

        public RoutePointService(IRoutePointRepository routePointRepository)
        {
            _routePointRepository = routePointRepository;
        }

        public async Task<RoutePointDto> CreateAsync(CreateRoutePointRequest request)
        {
            var rp = new RoutePoint(request.RouteId,new Coordinates(request.Latitude, request.Longitude), request.Order);
            // Assuming RouteId is set via navigation or foreign key property
            rp.RouteId = request.RouteId;
            await _routePointRepository.AddAsync(rp);
            await _routePointRepository.SaveChangesAsync();
            return new RoutePointDto(rp.Id, request.RouteId, rp.Location.Latitude, rp.Location.Longitude, rp.Order);
        }

        public async Task<IEnumerable<RoutePointDto>> GetByRouteAsync(Guid routeId)
        {
            var points = await _routePointRepository.GetByRouteIdAsync(routeId);
            return points.Select(rp => new RoutePointDto(rp.Id, routeId, rp.Location.Latitude, rp.Location.Longitude, rp.Order));
        }

        public async Task<RoutePointDto> GetByIdAsync(Guid id)
        {
            var rp = await _routePointRepository.GetByIdAsync(id);
            return new RoutePointDto(rp.Id, rp.RouteId, rp.Location.Latitude, rp.Location.Longitude, rp.Order);
        }

        public async Task<RoutePointDto> UpdateAsync(Guid id, UpdateRoutePointRequest request)
        {
            var rp = await _routePointRepository.GetByIdAsync(id);
            rp.UpdateOrder(request.Order); // assume such method exists
            _routePointRepository.Update(rp);
            await _routePointRepository.SaveChangesAsync();
            return new RoutePointDto(rp.Id, rp.RouteId, rp.Location.Latitude, rp.Location.Longitude, rp.Order);
        }

        public async Task DeleteAsync(Guid id)
        {
            var rp = await _routePointRepository.GetByIdAsync(id);
            _routePointRepository.Remove(rp);
            await _routePointRepository.SaveChangesAsync();
        }
    }
}
