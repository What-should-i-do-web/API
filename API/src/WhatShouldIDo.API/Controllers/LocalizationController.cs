using Microsoft.AspNetCore.Mvc;
using WhatShouldIDo.Application.Services;

namespace WhatShouldIDo.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LocalizationController : ControllerBase
{
    private readonly ILocalizationService _localizationService;

    public LocalizationController(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    [HttpGet("cultures")]
    public ActionResult<object> GetSupportedCultures()
    {
        return Ok(new
        {
            supported = _localizationService.GetSupportedCultures(),
            default_culture = _localizationService.GetDefaultCulture(),
            current_culture = _localizationService.GetUserCultureFromRequest()
        });
    }

    [HttpGet("test")]
    public async Task<ActionResult<object>> TestLocalization([FromQuery] string? culture = null)
    {
        culture ??= _localizationService.GetUserCultureFromRequest();

        var testTranslations = new
        {
            culture,
            translations = new
            {
                restaurant = await _localizationService.GetLocalizedTextAsync("category.restaurant", culture),
                cafe = await _localizationService.GetLocalizedTextAsync("category.cafe", culture),
                museum = await _localizationService.GetLocalizedTextAsync("category.museum", culture),
                highly_rated = await _localizationService.GetLocalizedTextAsync("reason.highly_rated", culture),
                popular = await _localizationService.GetLocalizedTextAsync("reason.popular", culture),
                morning = await _localizationService.GetLocalizedTextAsync("time.morning", culture),
                sunny = await _localizationService.GetLocalizedTextAsync("weather.sunny", culture)
            }
        };

        return Ok(testTranslations);
    }
}