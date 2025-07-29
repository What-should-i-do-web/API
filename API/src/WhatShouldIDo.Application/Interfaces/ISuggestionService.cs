using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.DTOs.Response;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface ISuggestionService
    {
        Task<List<SuggestionDto>> GetNearbySuggestionsAsync(float lat, float lng, int radius);
        Task<SuggestionDto> GetRandomSuggestionAsync(float lat, float lng, int radius);
        Task<List<SuggestionDto>> GetPromptSuggestionsAsync(PromptRequest request);
    }
}
