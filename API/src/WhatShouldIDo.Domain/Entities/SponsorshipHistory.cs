using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Domain.Entities
{
    public class SponsorshipHistory
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PlaceId { get; set; }
        public DateTime SponsoredAt { get; set; }
        public DateTime? SponsoredUntil { get; set; }
        public string Package { get; set; } // Gold / Silver vs.
    }
}
