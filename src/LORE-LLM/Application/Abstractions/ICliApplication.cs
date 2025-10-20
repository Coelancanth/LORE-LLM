using System.Threading.Tasks;

namespace LORE_LLM.Application.Abstractions;

public interface ICliApplication
{
    Task<int> RunAsync(string[] args);
}
