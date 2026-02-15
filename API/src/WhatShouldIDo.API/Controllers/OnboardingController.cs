using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WhatShouldIDo.API.Models;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.API.Controllers
{
    /// <summary>
    /// Onboarding endpoints for taste quiz and profile setup.
    /// Supports both authenticated and anonymous users.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class OnboardingController : ControllerBase
    {
        private readonly ITasteQuizService _quizService;
        private readonly ILogger<OnboardingController> _logger;

        public OnboardingController(
            ITasteQuizService quizService,
            ILogger<OnboardingController> logger)
        {
            _quizService = quizService;
            _logger = logger;
        }

        /// <summary>
        /// Get the current taste quiz definition.
        /// </summary>
        /// <returns>Server-driven quiz with localized content</returns>
        [HttpGet("taste-quiz")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(TasteQuizDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetTasteQuiz(CancellationToken cancellationToken)
        {
            var quiz = await _quizService.GetQuizAsync(cancellationToken);
            return Ok(quiz);
        }

        /// <summary>
        /// Submit taste quiz answers.
        /// For authenticated users: creates/updates profile immediately.
        /// For anonymous users: saves draft to Redis with claim token.
        /// </summary>
        /// <param name="request">Quiz submission with answers</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Profile summary and optional claim token</returns>
        [HttpPost("taste-quiz")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(TasteQuizSubmitResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> SubmitTasteQuiz(
            [FromBody] TasteQuizSubmitRequest request,
            CancellationToken cancellationToken)
        {
            // Get user ID if authenticated
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            Guid? userId = userIdClaim != null ? Guid.Parse(userIdClaim) : null;

            // Submit quiz
            var response = await _quizService.SubmitQuizAsync(
                request,
                userId,
                cancellationToken
            );

            if (userId.HasValue)
            {
                _logger.LogInformation(
                    "User {UserId} completed taste quiz version {Version}",
                    userId.Value,
                    request.QuizVersion
                );
            }
            else
            {
                _logger.LogInformation(
                    "Anonymous user completed taste quiz, draft saved with token (expires: {ExpiresAt})",
                    response.ClaimTokenExpiresAt
                );
            }

            return Ok(response);
        }

        /// <summary>
        /// Claim an anonymous taste quiz draft.
        /// Requires authentication.
        /// </summary>
        /// <param name="request">Claim token from anonymous quiz</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Claimed profile</returns>
        [HttpPost("claim-profile")]
        [Authorize]
        [ProducesResponseType(typeof(TasteProfileDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> ClaimProfile(
            [FromBody] TasteQuizClaimRequest request,
            CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userIdClaim == null)
            {
                return Unauthorized(new ErrorResponse
                {
                    Status = 401,
                    Title = "Unauthorized",
                    Detail = "User ID not found in token"
                });
            }

            var userId = Guid.Parse(userIdClaim);

            // Claim draft
            var claimResult = await _quizService.ClaimDraftAsync(request, userId, cancellationToken);

            if (!claimResult.Success || claimResult.Profile == null)
            {
                return NotFound(new ErrorResponse
                {
                    Status = 404,
                    Title = "Draft Not Found",
                    Detail = claimResult.ErrorMessage ?? "The claim token is invalid or has expired. Please complete the quiz again."
                });
            }

            _logger.LogInformation(
                "User {UserId} claimed anonymous taste profile with token",
                userId
            );

            return Ok(claimResult.Profile);
        }
    }
}
