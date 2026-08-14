namespace VoiceKassa.Application.Interfaces;

/// <summary>
/// Answers a free-form question ("bugun qancha savdo bo'ldi?") using
/// only the factual data passed in as context - the implementation
/// must not let the model invent numbers that weren't provided.
/// </summary>
public interface IAiQueryService
{
    Task<string> AnswerAsync(string question, string dataContextJson, CancellationToken ct = default);
}
