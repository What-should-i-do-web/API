using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.DTOs.Prompt
{
    public class InterpretedPrompt
    {
        public string TextQuery { get; set; } = string.Empty; // "cheap vegan food"
        public string? LocationText { get; set; }              // "Kadıköy", null olabilir
        public string[] PricePreferences { get; set; } = [];   // örn: ["PRICE_LEVEL_INEXPENSIVE"]
    }
}
