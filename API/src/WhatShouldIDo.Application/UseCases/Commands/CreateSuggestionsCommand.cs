using MediatR;
using WhatShouldIDo.Application.Models;

namespace WhatShouldIDo.Application.UseCases.Commands
{
    /// <summary>
    /// MediatR command for intent-first suggestion orchestration.
    /// Routes to appropriate suggestion logic based on user intent.
    /// </summary>
    public class CreateSuggestionsCommand : IRequest<SuggestionsResult>
    {
        public CreateSuggestionsInput Input { get; }

        public CreateSuggestionsCommand(CreateSuggestionsInput input)
        {
            Input = input ?? throw new ArgumentNullException(nameof(input));
        }
    }
}
