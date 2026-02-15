using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.DTOs.Response
{
    public class PromptSuggestionDto
    {
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public float Latitude { get; set; }
        public float Longitude { get; set; }
        public string? PriceLevel { get; set; }
        public string? Rating { get; set; }
    }
}
