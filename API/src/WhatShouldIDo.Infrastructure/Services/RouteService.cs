using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Repositories;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class RouteService : IRouteService
    {
        private readonly IRouteRepository _routeRepository;

        public RouteService(IRouteRepository routeRepository)
        {
            _routeRepository = routeRepository;
        }

        public async Task<RouteDto> CreateAsync(CreateRouteRequest request)
        {
            // Use a default userId (Guid.Empty) if not provided - ideally should come from request
            var userId = Guid.Empty; // TODO: Get from authenticated user
            var route = new Route(request.Name, userId);
            await _routeRepository.AddAsync(route);
            await _routeRepository.SaveChangesAsync();
            return new RouteDto
            {
                Id = route.Id,
                Name = route.Name,
                CreatedAt = route.CreatedAt,
                UserId = route.UserId,
                TotalDistance = route.TotalDistance,
                EstimatedDuration = route.EstimatedDuration
            };
        }

        public async Task<IEnumerable<RouteDto>> GetAllAsync()
        {
            var routes = await _routeRepository.GetAllAsync();
            return routes.Select(r => new RouteDto
            {
                Id = r.Id,
                Name = r.Name,
                CreatedAt = r.CreatedAt,
                UserId = r.UserId,
                TotalDistance = r.TotalDistance,
                EstimatedDuration = r.EstimatedDuration
            });
        }

        public async Task<RouteDto> UpdateAsync(Guid id, UpdateRouteRequest request)
        {
            var route = await _routeRepository.GetByIdAsync(id);
            if (route == null) throw new InvalidOperationException($"Route with id {id} not found");

            route.UpdateName(request.Name);
            await _routeRepository.UpdateAsync(route);
            await _routeRepository.SaveChangesAsync();
            return new RouteDto
            {
                Id = route.Id,
                Name = route.Name,
                CreatedAt = route.CreatedAt,
                UserId = route.UserId,
                TotalDistance = route.TotalDistance,
                EstimatedDuration = route.EstimatedDuration
            };
        }

        public async Task DeleteAsync(Guid id)
        {
            await _routeRepository.DeleteAsync(id);
            await _routeRepository.SaveChangesAsync();
        }
    }
}
