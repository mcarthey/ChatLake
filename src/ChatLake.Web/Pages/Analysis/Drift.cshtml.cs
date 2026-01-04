using ChatLake.Core.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ChatLake.Web.Pages.Analysis;

public class DriftModel : PageModel
{
    private readonly IDriftDetectionService _drift;

    public IReadOnlyList<ProjectDriftSummaryDto> HighDriftProjects { get; private set; } = [];

    public DriftModel(IDriftDetectionService drift)
    {
        _drift = drift;
    }

    public async Task OnGetAsync()
    {
        HighDriftProjects = await _drift.GetHighDriftProjectsAsync(limit: 20);
    }
}
