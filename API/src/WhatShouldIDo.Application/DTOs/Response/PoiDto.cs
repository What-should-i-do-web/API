using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.DTOs.Response
{
    public record PoiDto(
        Guid Id,
        string Name,
        double Latitude,
        double Longitude,
        string? Description
    );
}
