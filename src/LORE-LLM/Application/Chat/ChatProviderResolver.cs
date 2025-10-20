using System;
using System.Collections.Generic;
using System.Linq;

namespace LORE_LLM.Application.Chat;

public sealed class ChatProviderResolver
{
    private readonly IReadOnlyList<IChatProvider> _providers;

    public ChatProviderResolver(IEnumerable<IChatProvider> providers)
    {
        _providers = providers.ToList();
    }

    public IChatProvider? Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}


