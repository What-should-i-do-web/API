using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface IDayPlanningService
    {
        Task<DayPlanDto> CreateDayPlanAsync(DayPlanRequest request, CancellationToken cancellationToken = default);
        Task<DayPlanDto> CreatePersonalizedDayPlanAsync(Guid userId, DayPlanRequest request, CancellationToken cancellationToken = default);
        Task<List<DayPlanDto>> GetSampleDayPlansAsync(float latitude, float longitude, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates an AI-driven personalized day plan using user embeddings and diversity algorithms.
        /// Balances exploitation (familiar preferences) vs exploration (novel experiences).
        /// </summary>
        /// <param name="userId">User ID for personalization</param>
        /// <param name="request">Day plan request parameters</param>
        /// <param name="diversityFactor">Epsilon value for ε-greedy (0.0-1.0). 0=only familiar, 1=only novel. Default: 0.2</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>AI-generated personalized day plan</returns>
        Task<DayPlanDto> CreateAIDrivenRouteAsync(
            Guid userId,
            DayPlanRequest request,
            double diversityFactor = 0.2,
            CancellationToken cancellationToken = default);
    }
}