
using Microsoft.AI.Agents.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Orleans.Runtime;

[ApiController]
public class Articles : ControllerBase
{
    private readonly IClusterClient _client;

    public Articles(IClusterClient client)
    {
        _client = client;
    }

    [HttpPost("/articles/{userId}")]
    public async Task<string> Create(string userId, [FromBody] ArticleRequest request)
    {
        var streamProvider = _client.GetStreamProvider("StreamProvider");

        var streamId = StreamId.Create(Consts.OrleansNamespace, userId);
        var stream = streamProvider.GetStream<Event>(streamId);

        var data = new Dictionary<string, string>
            {
                { "userMessage", request.Ask }
            };

        await stream.OnNextAsync(new Event
        {
            Type = nameof(EventTypes.UserChatInput),
            Data = data
        });

        return $"Task accepted";
    }
}

public class ArticleRequest
{
    public string Ask { get; set; }
}
