using Microsoft.EntityFrameworkCore;
using WhatShouldIDo.API.Middleware;
using WhatShouldIDo.API.Validators;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Data;
using WhatShouldIDo.Infrastructure.Repositories;
using WhatShouldIDo.Infrastructure.Services;
using Microsoft.OpenApi.Models;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using WhatShouldIDo.Infrastructure.Caching;
var builder = WebApplication.CreateBuilder(args);


//Read JWT settings from configuration
var jwtSection = builder.Configuration.GetSection("JwtSettings");
var jwtKey = jwtSection["Key"]!;
var jwtIssuer = jwtSection["Issuer"]!;
var jwtAudience = jwtSection["Audience"]!;
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
//Auth
builder.Services
  .AddAuthentication(options =>
  {
      options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
      options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
  })
  .AddJwtBearer(options =>
  {
      options.RequireHttpsMetadata = false;
      options.SaveToken = true;
      options.TokenValidationParameters = new TokenValidationParameters
      {
          ValidateIssuer = true,
          ValidIssuer = jwtIssuer,
          ValidateAudience = true,
          ValidAudience = jwtAudience,
          ValidateIssuerSigningKey = true,
          IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
          ValidateLifetime = true,
          ClockSkew = TimeSpan.Zero
      };
  });

//redis
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
});
builder.Services.AddScoped<RedisHealthChecker>();
builder.Services.AddScoped<ICacheService, RedisCacheService>();

// Database
builder.Services.AddDbContext<WhatShouldIDoDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IRouteRepository, RouteRepository>();

// Services
builder.Services.AddScoped<IRouteService, RouteService>();
builder.Services.AddScoped<IPoiRepository, PoiRepository>();
builder.Services.AddScoped<IRoutePointRepository, RoutePointRepository>();
builder.Services.AddScoped<IPoiService, PoiService>();
builder.Services.AddScoped<IRoutePointService, RoutePointService>();
builder.Services.AddScoped<ISuggestionService, SuggestionService>();
builder.Services.AddHttpClient<IPlacesProvider, GooglePlacesProvider>();
builder.Services.AddScoped<IPromptInterpreter, BasicPromptInterpreter>();
builder.Services.AddScoped<IGeocodingService, GoogleGeocodingService>();
builder.Services.AddScoped<IPlaceService, PlaceService>();

// Controllers
builder.Services.AddControllers();

// Auto‐ and client‐side validation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<CreateRouteRequestValidator>();



// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WhatShouldIDo API", Version = "v1" });
});



var googleApiKey = builder.Configuration["GooglePlaces:ApiKey"];


var app = builder.Build();





// Middleware pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WhatShouldIDo API v1");
    });
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();