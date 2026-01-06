using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatLake.Web.Pages.Projects;

public class SuggestionDetailModel : PageModel
{
    private readonly IProjectSuggestionService _suggestions;

    public ProjectSuggestionDto? Suggestion { get; private set; }
    public IReadOnlyList<ConversationPreviewDto> Conversations { get; private set; } = [];

    public SuggestionDetailModel(IProjectSuggestionService suggestions)
    {
        _suggestions = suggestions;
    }

    public async Task OnGetAsync(long id)
    {
        var all = await _suggestions.GetPendingSuggestionsAsync();
        Suggestion = all.FirstOrDefault(s => s.ProjectSuggestionId == id);

        // Also check other statuses if not found in pending
        if (Suggestion is null)
        {
            var accepted = await _suggestions.GetSuggestionsByStatusAsync("Accepted");
            Suggestion = accepted.FirstOrDefault(s => s.ProjectSuggestionId == id);
        }

        if (Suggestion is null)
        {
            var rejected = await _suggestions.GetSuggestionsByStatusAsync("Rejected");
            Suggestion = rejected.FirstOrDefault(s => s.ProjectSuggestionId == id);
        }

        if (Suggestion is not null)
        {
            Conversations = await _suggestions.GetSuggestionConversationsAsync(id);
        }
    }
}
