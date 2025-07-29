using System.Collections.Generic;
using WhatShouldIDo.Application.DTOs.Prompt;
using WhatShouldIDo.Application.Interfaces;
using System.Threading.Tasks;

namespace WhatShouldIDo.Application.Interfaces
{
    

    public interface IPromptInterpreter
    {
        Task<InterpretedPrompt> InterpretAsync(string promptText);
    }
}
