using System;

namespace WhatShouldIDo.API.Attributes
{
    /// <summary>
    /// Attribute to mark endpoints that require premium subscription.
    /// These endpoints are blocked for non-premium users regardless of quota availability.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class PremiumOnlyAttribute : Attribute
    {
    }
}
