using System.Diagnostics;
using ChatLake.Inference.Clustering;
using ChatLake.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UMAP;

namespace ChatLake.Web.Controllers;

[ApiController]
[Route("api/clustering")]
public class ClusteringApiController : ControllerBase
{
    private readonly ChatLakeDbContext _db;

    public ClusteringApiController(ChatLakeDbContext db)
    {
        _db = db;
    }

    [HttpGet("visualization")]
    public async Task<IActionResult> GetVisualization()
    {
        var stopwatch = Stopwatch.StartNew();

        // Load all segment embeddings
        var embeddings = await _db.SegmentEmbeddings
            .Include(e => e.ConversationSegment)
            .Select(e => new
            {
                e.ConversationSegmentId,
                e.ConversationSegment.ConversationId,
                e.ConversationSegment.ContentText,
                Embedding = e.EmbeddingVector
            })
            .ToListAsync();

        if (embeddings.Count == 0)
        {
            return Ok(new { points = Array.Empty<object>(), clusterCount = 0 });
        }

        // Load cluster assignments from all suggestions (pending + accepted)
        var allSuggestions = await _db.ProjectSuggestions
            .Where(s => s.Status == "Pending" || s.Status == "Accepted")
            .OrderByDescending(s => s.Confidence)
            .Select(s => new { s.SuggestedName, s.SegmentIdsJson })
            .ToListAsync();

        // Build segment -> cluster mapping
        var segmentToCluster = new Dictionary<long, (int ClusterId, string Name)>();
        var clusterNames = new Dictionary<string, string>();

        int clusterId = 1;
        foreach (var suggestion in allSuggestions)
        {
            var name = suggestion.SuggestedName;
            clusterNames[clusterId.ToString()] = name.Length > 25 ? name[..25] + "..." : name;

            var segmentIds = System.Text.Json.JsonSerializer.Deserialize<List<long>>(suggestion.SegmentIdsJson ?? "[]") ?? [];
            foreach (var segId in segmentIds)
            {
                if (!segmentToCluster.ContainsKey(segId)) // First cluster wins (highest confidence)
                {
                    segmentToCluster[segId] = (clusterId, name);
                }
            }
            clusterId++;
        }

        // Convert embeddings to float arrays
        var vectors = embeddings
            .Select(e => DeserializeEmbedding(e.Embedding))
            .ToArray();

        // Run UMAP to 2D for visualization
        var umap = new Umap(
            distance: Umap.DistanceFunctions.Cosine,
            dimensions: 2,
            numberOfNeighbors: Math.Min(15, embeddings.Count - 1),
            random: new SeededRandomProvider(42));

        var epochs = umap.InitializeFit(vectors);
        for (int i = 0; i < epochs; i++)
        {
            umap.Step();
        }

        var reduced = umap.GetEmbedding();
        stopwatch.Stop();

        // Build response
        var points = new List<object>();
        for (int i = 0; i < embeddings.Count; i++)
        {
            var segmentId = embeddings[i].ConversationSegmentId;
            var (clusterIdAssigned, _) = segmentToCluster.GetValueOrDefault(segmentId, (0, "Noise"));

            var preview = embeddings[i].ContentText ?? "";
            if (preview.Length > 100)
                preview = preview[..100] + "...";
            preview = preview.Replace("\n", " ").Replace("\r", "");

            points.Add(new
            {
                segmentId,
                conversationId = embeddings[i].ConversationId,
                x = reduced[i][0],
                y = reduced[i][1],
                clusterId = clusterIdAssigned,
                preview
            });
        }

        return Ok(new
        {
            points,
            clusterCount = clusterNames.Count,
            clusterNames,
            umapDurationMs = stopwatch.ElapsedMilliseconds
        });
    }

    private static float[] DeserializeEmbedding(byte[] bytes)
    {
        var floats = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }
}
