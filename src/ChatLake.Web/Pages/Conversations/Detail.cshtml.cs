using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatLake.Web.Pages.Conversations;

public class DetailModel : PageModel
{
    private readonly IConversationQueryService _queries;
    private readonly ITopicExtractionService _topics;
    private readonly ISimilarityService _similarity;

    public ConversationDetailDto Conversation { get; private set; } = null!;
    public IReadOnlyList<ConversationTopicDto> Topics { get; private set; } = [];
    public IReadOnlyList<SimilarConversationDto> SimilarConversations { get; private set; } = [];

    public DetailModel(
        IConversationQueryService queries,
        ITopicExtractionService topics,
        ISimilarityService similarity)
    {
        _queries = queries;
        _topics = topics;
        _similarity = similarity;
    }

    public async Task OnGetAsync(long id)
    {
        Conversation = await _queries.GetConversationAsync(id);
        Topics = await _topics.GetConversationTopicsAsync(id);
        SimilarConversations = await _similarity.FindSimilarAsync(id, limit: 5);
    }
}
