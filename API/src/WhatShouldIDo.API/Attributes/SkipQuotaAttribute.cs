using System;

namespace WhatShouldIDo.API.Attributes
{
    /// <summary>
    /// Attribute to mark endpoints that should bypass quota enforcement.
    /// Use for non-feature endpoints like profile retrieval, health checks, etc.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class SkipQuotaAttribute : Attribute
    {
    }
}
