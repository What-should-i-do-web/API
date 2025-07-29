using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Domain.Entities;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    public interface IPoiRepository : IGenericRepository<Poi>
    {
        // Additional POI-specific methods if needed
    }
}
