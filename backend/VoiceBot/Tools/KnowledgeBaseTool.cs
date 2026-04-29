using System.Text.Json;
using Azure.AI.VoiceLive;

/// <summary>
/// Searches the local Atlas Airways knowledge base files for relevant policy content.
/// All data is loaded from markdown files in Data/ at startup.
/// </summary>
public sealed class KnowledgeBaseTool
{
    private readonly Dictionary<string, string> _knowledgeBase = new(StringComparer.OrdinalIgnoreCase);

    public KnowledgeBaseTool()
    {
        var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dataDir))
            dataDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");

        if (Directory.Exists(dataDir))
        {
            foreach (var file in Directory.GetFiles(dataDir, "atlas-*.md"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                _knowledgeBase[name] = File.ReadAllText(file);
            }
        }
    }

    public VoiceLiveFunctionDefinition Definition => new("search_knowledge_base")
    {
        Description = "Search the Atlas Airways knowledge base for travel policies, baggage rules, FAQ, contact info, and other official Atlas Airways content. Use this whenever a customer asks about Atlas Airways policies or general travel information.",
        Parameters = BinaryData.FromObjectAsJson(new
        {
            type = "object",
            properties = new
            {
                query = new
                {
                    type = "string",
                    description = "The search query describing what the customer is asking about, e.g. 'cabin baggage dimensions', 'sports equipment policy', 'SkyPoints'"
                }
            },
            required = new[] { "query" }
        })
    };

    public object Execute(JsonElement args)
    {
        var query = args.GetProperty("query").GetString() ?? "";
        var results = Search(query);

        if (results.Count == 0)
            return new { found = false, message = "No matching policy content found." };

        return new { found = true, results };
    }

    private List<object> Search(string query)
    {
        var queryLower = query.ToLowerInvariant();
        var matches = new List<object>();

        foreach (var (name, content) in _knowledgeBase)
        {
            // Simple keyword matching — score by how many query words appear in the content
            var words = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var contentLower = content.ToLowerInvariant();
            var matchCount = words.Count(w => contentLower.Contains(w));

            if (matchCount > 0)
            {
                matches.Add(new
                {
                    source = name,
                    relevance = matchCount,
                    content = content.Length > 2000 ? content[..2000] + "..." : content
                });
            }
        }

        return matches
            .OrderByDescending(m => ((dynamic)m).relevance)
            .Take(3)
            .ToList();
    }
}
