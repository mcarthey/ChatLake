using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatLake.Web.Pages.Conversations;

public class DetailModel : PageModel
{
    private readonly IConversationQueryService _queries;

    public ConversationDetailDto Conversation { get; private set; } = null!;

    public DetailModel(IConversationQueryService queries)
    {
        _queries = queries;
    }

    public async Task OnGetAsync(long id)
    {
        Conversation = await _queries.GetConversationAsync(id);
    }
}
