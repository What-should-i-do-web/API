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
            var route = new Route(request.Name);
            await _routeRepository.AddAsync(route);
            await _routeRepository.SaveChangesAsync();
            return new RouteDto(route.Id, route.Name, route.CreatedAt);
        }

        public async Task<IEnumerable<RouteDto>> GetAllAsync()
        {
            var routes = await _routeRepository.GetAllAsync();
            return routes.Select(r => new RouteDto(r.Id, r.Name, r.CreatedAt));
        }

        public async Task<RouteDto> UpdateAsync(Guid id, UpdateRouteRequest request)
        {
            var route = await _routeRepository.GetByIdAsync(id);
            route.UpdateName(request.Name);
            _routeRepository.Update(route);
            await _routeRepository.SaveChangesAsync();
            return new RouteDto(route.Id, route.Name, route.CreatedAt);
        }

        public async Task DeleteAsync(Guid id)
        {
            var route = await _routeRepository.GetByIdAsync(id);
            _routeRepository.Remove(route);
            await _routeRepository.SaveChangesAsync();
        }
    }
}
