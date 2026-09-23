namespace AspireAiStack.ApiService;

public sealed record ChatRequest(string Prompt);

public sealed record SourceCitation(string Title, string Content, float Relevance);

public sealed record ChatResponse(
    string Answer,
    string AiMode,
    string VectorStore,
    string Cache,
    bool CacheHit,
    IReadOnlyList<SourceCitation> Sources);

public sealed record StackStatus(
    string AiMode,
    string VectorStore,
    string Cache,
    bool SafeDefault);

public sealed record KnowledgeDocument(
    ulong Id,
    string Title,
    string Content,
    string SuggestedQuestion);

public sealed record KnowledgeTopic(string Title, string SuggestedQuestion);
