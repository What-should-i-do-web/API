using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface IDayPlanningService
    {
        Task<DayPlanDto> CreateDayPlanAsync(DayPlanRequest request, CancellationToken cancellationToken = default);
        Task<DayPlanDto> CreatePersonalizedDayPlanAsync(Guid userId, DayPlanRequest request, CancellationToken cancellationToken = default);
        Task<List<DayPlanDto>> GetSampleDayPlansAsync(float latitude, float longitude, CancellationToken cancellationToken = default);
    }
}