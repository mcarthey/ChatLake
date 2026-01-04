using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatLake.Web.Pages.Projects;

public class DetailModel : PageModel
{
    private readonly IProjectService _projects;

    public ProjectDetailDto? Project { get; private set; }
    public IReadOnlyList<ProjectConversationDto> Conversations { get; private set; } = [];

    public DetailModel(IProjectService projects)
    {
        _projects = projects;
    }

    public async Task<IActionResult> OnGetAsync(long id)
    {
        Project = await _projects.GetByIdAsync(id);

        if (Project is null)
            return NotFound();

        Conversations = await _projects.GetProjectConversationsAsync(id);
        return Page();
    }
}
