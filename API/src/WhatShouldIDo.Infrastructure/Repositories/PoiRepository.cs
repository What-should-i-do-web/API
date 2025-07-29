using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WhatShouldIDo.Domain.Entities;
using WhatShouldIDo.Infrastructure.Data;

namespace WhatShouldIDo.Infrastructure.Repositories
{
    public class PoiRepository : GenericRepository<Poi>, IPoiRepository
    {
        public PoiRepository(WhatShouldIDoDbContext dbContext) : base(dbContext) { }
    }
}
