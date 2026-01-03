using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class IndexModel : PageModel
{
    private readonly IConversationQueryService _queries;

    public IReadOnlyList<ConversationSummaryDto> Conversations { get; private set; } = [];

    public IndexModel(IConversationQueryService queries)
    {
        _queries = queries;
    }

    public async Task OnGetAsync()
    {
        Conversations = await _queries.GetAllSummariesAsync();
    }
}
