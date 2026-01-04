using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatLake.Web.Pages.Analysis;

public class SearchModel : PageModel
{
    private readonly ISimilarityService _similarity;

    [BindProperty]
    public string? Query { get; set; }

    public IReadOnlyList<SimilarConversationDto> Results { get; private set; } = [];

    public SearchModel(ISimilarityService similarity)
    {
        _similarity = similarity;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            return Page();
        }

        Results = await _similarity.SearchSimilarAsync(Query, limit: 20);
        return Page();
    }
}
