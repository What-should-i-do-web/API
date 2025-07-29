using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    public record UpdatePoiRequest(
        string Name,
        double Latitude,
        double Longitude,
        string? Description = null
    );
}
