using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface IUserService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
        Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
        Task<UserDto> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<UserDto> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
        Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default);
        Task<bool> IncrementApiUsageAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> CanUserMakeApiCallAsync(Guid userId, CancellationToken cancellationToken = default);
        Task ResetDailyUsageAsync(CancellationToken cancellationToken = default);
        Task<bool> UpgradeSubscriptionAsync(Guid userId, SubscriptionTier tier, DateTime? expiry = null, CancellationToken cancellationToken = default);
    }
}