using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.DTOs.Requests
{
    public record UpdateRoutePointRequest(
         double Latitude,
         double Longitude,
         int Order
     );
}
