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
            var location = new Coordinates(request.Latitude, request.Longitude);
            var rp = new RoutePoint(request.RouteId, location, request.Order);
            await _routePointRepository.AddAsync(rp);
            await _routePointRepository.SaveChangesAsync();
            return new RoutePointDto
            {
                Id = rp.Id,
                RouteId = request.RouteId,
                Latitude = rp.Location.Latitude,
                Longitude = rp.Location.Longitude,
                Order = rp.Order
            };
        }

        public async Task<IEnumerable<RoutePointDto>> GetByRouteAsync(Guid routeId)
        {
            var points = await _routePointRepository.GetByRouteIdAsync(routeId);
            return points.Select(rp => new RoutePointDto
            {
                Id = rp.Id,
                RouteId = routeId,
                Latitude = rp.Location.Latitude,
                Longitude = rp.Location.Longitude,
                Order = rp.Order
            });
        }

        public async Task<RoutePointDto> GetByIdAsync(Guid id)
        {
            var rp = await _routePointRepository.GetByIdAsync(id);
            return new RoutePointDto
            {
                Id = rp.Id,
                RouteId = rp.RouteId,
                Latitude = rp.Location.Latitude,
                Longitude = rp.Location.Longitude,
                Order = rp.Order
            };
        }

        public async Task<RoutePointDto> UpdateAsync(Guid id, UpdateRoutePointRequest request)
        {
            var rp = await _routePointRepository.GetByIdAsync(id);
            rp.UpdateOrder(request.Order);
            _routePointRepository.Update(rp);
            await _routePointRepository.SaveChangesAsync();
            return new RoutePointDto
            {
                Id = rp.Id,
                RouteId = rp.RouteId,
                Latitude = rp.Location.Latitude,
                Longitude = rp.Location.Longitude,
                Order = rp.Order
            };
        }

        public async Task DeleteAsync(Guid id)
        {
            var rp = await _routePointRepository.GetByIdAsync(id);
            _routePointRepository.Remove(rp);
            await _routePointRepository.SaveChangesAsync();
        }
    }
}
