using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    public class PromptRequest
    {
        public string Prompt { get; set; } = string.Empty;

        // Kullanıcıdan gönderiliyorsa eklenebilir (location override etmeyebilir)
        public float? Latitude { get; set; }
        public float? Longitude { get; set; }
        public int Radius { get; set; } = 3000; // varsayılan 3km
        public string? SortBy { get; set; }
    }
}
