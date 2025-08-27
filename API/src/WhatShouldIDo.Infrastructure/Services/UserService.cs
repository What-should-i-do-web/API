using BCrypt.Net;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Domain.Entities;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace WhatShouldIDo.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserService> _logger;
        private readonly IConfiguration _configuration;

        public UserService(IUserRepository userRepository, ILogger<UserService> logger, IConfiguration configuration)
        {
            _userRepository = userRepository;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check if email already exists
                if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
                {
                    throw new InvalidOperationException("Email is already registered");
                }

                // Check if username already exists
                if (await _userRepository.ExistsByUsernameAsync(request.UserName, cancellationToken))
                {
                    throw new InvalidOperationException("Username is already taken");
                }

                // Create new user
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = request.Email.ToLower(),
                    UserName = request.UserName,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, BCrypt.Net.BCrypt.GenerateSalt(12)),
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    SubscriptionTier = SubscriptionTier.Free,
                    IsActive = true
                };

                // Create user in database
                user = await _userRepository.CreateAsync(user, cancellationToken);

                // Create user profile if additional info provided
                if (!string.IsNullOrWhiteSpace(request.City) || !string.IsNullOrWhiteSpace(request.Country))
                {
                    var profile = new UserProfile
                    {
                        Id = Guid.NewGuid(),
                        UserId = user.Id,
                        City = request.City,
                        Country = request.Country,
                        Language = request.Language ?? "en",
                        IsLocal = request.IsLocal
                    };

                    // Note: You'll need to add UserProfile repository or add to context directly
                    // For now, we'll update the user entity directly
                }

                // Generate JWT token
                var token = GenerateJwtToken(user);
                var userDto = MapToUserDto(user);

                _logger.LogInformation("User registered successfully: {UserId} - {Email}", user.Id, user.Email);

                return new AuthResponse
                {
                    Token = token,
                    Expiry = DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes()),
                    User = userDto,
                    Message = "Registration successful"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user registration: {Email}", request.Email);
                throw;
            }
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                // Get user by email
                var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
                if (user == null)
                {
                    _logger.LogWarning("Login attempt with non-existent email: {Email}", request.Email);
                    throw new UnauthorizedAccessException("Invalid email or password");
                }

                // Verify password
                if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    _logger.LogWarning("Failed login attempt for user: {UserId} - {Email}", user.Id, user.Email);
                    throw new UnauthorizedAccessException("Invalid email or password");
                }

                // Check if subscription is expired and downgrade if needed
                await CheckAndUpdateExpiredSubscription(user, cancellationToken);

                // Generate JWT token
                var token = GenerateJwtToken(user);
                var userDto = MapToUserDto(user);

                _logger.LogInformation("User logged in successfully: {UserId} - {Email}", user.Id, user.Email);

                return new AuthResponse
                {
                    Token = token,
                    Expiry = DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes()),
                    User = userDto,
                    Message = "Login successful"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during user login: {Email}", request.Email);
                throw;
            }
        }

        public async Task<UserDto> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _userRepository.GetWithProfileAsync(userId, cancellationToken);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                return MapToUserDto(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by ID: {UserId}", userId);
                throw;
            }
        }

        public async Task<UserDto> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _userRepository.GetByEmailAsync(email, cancellationToken);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with email {email} not found");
                }

                return MapToUserDto(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user by email: {Email}", email);
                throw;
            }
        }

        public async Task<UserDto> UpdateProfileAsync(Guid userId, UpdateProfileRequest request, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _userRepository.GetWithProfileAsync(userId, cancellationToken);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                // Update user basic info
                if (!string.IsNullOrWhiteSpace(request.FirstName))
                    user.FirstName = request.FirstName;
                if (!string.IsNullOrWhiteSpace(request.LastName))
                    user.LastName = request.LastName;
                if (!string.IsNullOrWhiteSpace(request.BudgetRange))
                    user.BudgetRange = request.BudgetRange;
                if (!string.IsNullOrWhiteSpace(request.MobilityLevel))
                    user.MobilityLevel = request.MobilityLevel;
                if (!string.IsNullOrWhiteSpace(request.PreferredCuisines))
                    user.PreferredCuisines = request.PreferredCuisines;
                if (!string.IsNullOrWhiteSpace(request.ActivityPreferences))
                    user.ActivityPreferences = request.ActivityPreferences;

                // Update user profile
                if (user.Profile != null)
                {
                    UpdateUserProfile(user.Profile, request);
                }

                user = await _userRepository.UpdateAsync(user, cancellationToken);
                
                _logger.LogInformation("User profile updated: {UserId}", userId);
                return MapToUserDto(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user profile: {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> IncrementApiUsageAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var newUsage = await _userRepository.IncrementApiUsageAsync(userId, cancellationToken);
                _logger.LogDebug("API usage incremented for user {UserId}: {Usage}", userId, newUsage);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error incrementing API usage for user: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> CanUserMakeApiCallAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
                if (user == null)
                    return false;

                var dailyLimit = GetDailyApiLimit(user.SubscriptionTier);
                
                _logger.LogDebug("Checking API limit for user {UserId}: {Usage}/{Limit}", 
                    userId, user.DailyApiUsage, dailyLimit);
                
                return user.DailyApiUsage < dailyLimit;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking API usage limit for user: {UserId}", userId);
                return false;
            }
        }

        public async Task ResetDailyUsageAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var count = await _userRepository.ResetDailyUsageForAllUsersAsync(cancellationToken);
                _logger.LogInformation("Daily API usage reset completed for {Count} users", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during daily usage reset");
                throw;
            }
        }

        public async Task<bool> UpgradeSubscriptionAsync(Guid userId, SubscriptionTier tier, DateTime? expiry = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
                if (user == null)
                    return false;

                user.SubscriptionTier = tier;
                user.SubscriptionExpiry = expiry;

                await _userRepository.UpdateAsync(user, cancellationToken);
                
                _logger.LogInformation("User subscription updated: {UserId} - {Tier}", userId, tier);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upgrading subscription for user: {UserId}", userId);
                return false;
            }
        }

        // Private helper methods
        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var keyBytes = Encoding.UTF8.GetBytes(jwtSettings["Key"]!);
            var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("username", user.UserName),
                new Claim("subscription_tier", user.SubscriptionTier.ToString()),
                new Claim("daily_usage", user.DailyApiUsage.ToString())
            };

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(GetJwtExpiryMinutes()),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private int GetJwtExpiryMinutes()
        {
            return int.Parse(_configuration["JwtSettings:DurationInMinutes"] ?? "60");
        }

        private static int GetDailyApiLimit(SubscriptionTier tier) => tier switch
        {
            SubscriptionTier.Free => 5,
            SubscriptionTier.Pro => 50,
            SubscriptionTier.Business => 200,
            _ => 5
        };

        private async Task CheckAndUpdateExpiredSubscription(User user, CancellationToken cancellationToken)
        {
            if (user.SubscriptionExpiry.HasValue && user.SubscriptionExpiry < DateTime.UtcNow && user.SubscriptionTier != SubscriptionTier.Free)
            {
                user.SubscriptionTier = SubscriptionTier.Free;
                user.SubscriptionExpiry = null;
                await _userRepository.UpdateAsync(user, cancellationToken);
                
                _logger.LogInformation("User subscription expired and downgraded to Free: {UserId}", user.Id);
            }
        }

        private static void UpdateUserProfile(UserProfile profile, UpdateProfileRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.City))
                profile.City = request.City;
            if (!string.IsNullOrWhiteSpace(request.Country))
                profile.Country = request.Country;
            if (!string.IsNullOrWhiteSpace(request.Language))
                profile.Language = request.Language;
            if (request.Age.HasValue)
                profile.Age = request.Age;
            if (!string.IsNullOrWhiteSpace(request.TravelStyle))
                profile.TravelStyle = request.TravelStyle;
            if (!string.IsNullOrWhiteSpace(request.CompanionType))
                profile.CompanionType = request.CompanionType;
            if (request.IsLocal.HasValue)
                profile.IsLocal = request.IsLocal.Value;
            if (request.PreferredRadius.HasValue)
                profile.PreferredRadius = request.PreferredRadius;
            if (request.TypicalBudgetPerDay.HasValue)
                profile.TypicalBudgetPerDay = request.TypicalBudgetPerDay;

            // Update JSON preferences
            if (!string.IsNullOrWhiteSpace(request.FavoriteCuisines))
                profile.FavoriteCuisines = request.FavoriteCuisines;
            if (!string.IsNullOrWhiteSpace(request.FavoriteActivityTypes))
                profile.FavoriteActivityTypes = request.FavoriteActivityTypes;
            if (!string.IsNullOrWhiteSpace(request.AvoidedActivityTypes))
                profile.AvoidedActivityTypes = request.AvoidedActivityTypes;
            if (!string.IsNullOrWhiteSpace(request.TimePreferences))
                profile.TimePreferences = request.TimePreferences;

            profile.UpdatedAt = DateTime.UtcNow;
        }

        private static UserDto MapToUserDto(User user)
        {
            var userDto = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                UserName = user.UserName,
                FirstName = user.FirstName,
                LastName = user.LastName,
                SubscriptionTier = user.SubscriptionTier,
                SubscriptionExpiry = user.SubscriptionExpiry,
                DailyApiUsage = user.DailyApiUsage,
                CreatedAt = user.CreatedAt,
                IsActive = user.IsActive
            };

            if (user.Profile != null)
            {
                userDto.Profile = new UserProfileDto
                {
                    City = user.Profile.City,
                    Country = user.Profile.Country,
                    Language = user.Profile.Language,
                    TravelStyle = user.Profile.TravelStyle,
                    IsLocal = user.Profile.IsLocal,
                    PreferredRadius = user.Profile.PreferredRadius,
                    PersonalizationScore = user.Profile.PersonalizationScore
                };
            }

            return userDto;
        }
    }
}