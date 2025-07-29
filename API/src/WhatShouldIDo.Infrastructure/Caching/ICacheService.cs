using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatShouldIDo.Infrastructure.Caching
{
    public interface ICacheService
    {
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> acquire, TimeSpan? absoluteExpiration = null);
        Task RemoveAsync(string key);
    }
}
