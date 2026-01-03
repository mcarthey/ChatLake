using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatLake.Web.Pages.Projects;

public class IndexModel : PageModel
{
    private readonly IProjectService _projects;

    public IReadOnlyList<ProjectDto> Projects { get; private set; } = [];

    public IndexModel(IProjectService projects)
    {
        _projects = projects;
    }

    public async Task OnGetAsync()
    {
        Projects = await _projects.ListAsync();
    }
}
