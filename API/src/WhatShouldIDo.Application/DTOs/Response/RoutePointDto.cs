using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.DTOs.Response
{
    public record RoutePointDto(
        Guid Id,
        Guid RouteId,
        double Latitude,
        double Longitude,
        int Order
    );
}
