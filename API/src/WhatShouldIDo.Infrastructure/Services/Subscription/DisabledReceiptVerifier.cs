using WhatShouldIDo.Application.DTOs.Requests;
using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services.Subscription
{
    /// <summary>
    /// Receipt verifier that always returns disabled status.
    /// Used when VerificationEnabled is false.
    /// </summary>
    public class DisabledReceiptVerifier : IReceiptVerifier
    {
        public Task<ReceiptVerificationResult> VerifyAsync(
            VerifyReceiptRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(ReceiptVerificationResult.Disabled());
        }
    }
}
