using Microsoft.EntityFrameworkCore;
using WhatShouldIDo.API.Middleware;
using WhatShouldIDo.API.Validators;
using WhatShouldIDo.Application.Interfaces;
using WhatShouldIDo.Infrastructure.Data;
using WhatShouldIDo.Infrastructure.Repositories;
using WhatShouldIDo.Infrastructure.Services;
using Microsoft.OpenApi.Models;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<WhatShouldIDoDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Repositories
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IRouteRepository, RouteRepository>();

// Services
builder.Services.AddScoped<IRouteService, RouteService>();

// Controllers
builder.Services.AddControllers()
    .AddFluentValidation(config =>
    {
        config.AutomaticValidationEnabled = true;
        config.RegisterValidatorsFromAssemblyContaining<CreateRouteRequestValidator>();
    });
// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WhatShouldIDo API", Version = "v1" });
});

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

app.UseAuthorization();
app.MapControllers();
app.Run();