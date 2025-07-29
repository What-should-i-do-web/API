using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Application.DTOs.Requests;

namespace WhatShouldIDo.Application.Interfaces
{
    public interface IPlaceService
    {
        Task<bool> UpdateSponsorshipAsync(UpdatePlaceSponsorshipRequest request);
    }
}
