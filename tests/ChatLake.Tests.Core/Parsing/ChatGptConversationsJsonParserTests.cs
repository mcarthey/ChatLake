using ChatLake.Core.Parsing;
using ChatLake.Infrastructure.Parsing.ChatGpt;

namespace ChatLake.Tests.Core.Parsing;

public sealed class ChatGptConversationsJsonParserTests : RawArtifactParserContractTests
{
    protected override IRawArtifactParser CreateParser()
        => new ChatGptConversationsJsonParser();

    protected override string ValidJsonSample => """
    [
      {
        "id": "conv_1",
        "title": "Test",
        "create_time": 1700000000,
        "update_time": 1700000100,
        "mapping": {
          "node0": { "id": "node0", "message": null, "parent": null, "children": ["node1"] },
          "node1": {
            "id": "node1",
            "message": {
              "id": "msg1",
              "author": { "role": "user" },
              "create_time": 1700000001,
              "content": { "content_type": "text", "parts": ["Hello"] }
            },
            "parent": "node0",
            "children": ["node2"]
          },
          "node2": {
            "id": "node2",
            "message": {
              "id": "msg2",
              "author": { "role": "assistant" },
              "create_time": 1700000002,
              "content": { "content_type": "text", "parts": ["Hi there"] }
            },
            "parent": "node1",
            "children": []
          }
        },
        "current_node": "node2"
      }
    ]
    """;
}
