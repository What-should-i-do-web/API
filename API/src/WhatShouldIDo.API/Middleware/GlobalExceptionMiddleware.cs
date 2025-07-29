using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using WhatShouldIDo.Domain.Exceptions;
using WhatShouldIDo.API.Models;

namespace WhatShouldIDo.API.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (DomainException dex)
            {
                _logger.LogWarning(dex, "Domain error occurred");
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.ContentType = "application/json";
                var error = new ErrorResponse("Domain Exception \n",dex.Message);
                await context.Response.WriteAsJsonAsync(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception");
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";
                var error = new ErrorResponse("An unexpected error occurred.\n",ex.ToString());
                await context.Response.WriteAsJsonAsync(error);
            }
        }
    }
}