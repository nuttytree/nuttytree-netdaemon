using Refit;

namespace NuttyTree.NetDaemon.ExternalServices.RandomWords;

public interface IRandomWordApi
{
    [Get("/word")]
    Task<List<string>> GetRandomWordsAsync(int? number = 1, int? length = null, [AliasAs("lang")] string language = "en");
}
