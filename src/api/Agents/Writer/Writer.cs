
using Microsoft.AI.Agents.Abstractions;
using Microsoft.AI.Agents.Orleans;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Orleans.Runtime;

[ImplicitStreamSubscription(Consts.OrleansNamespace)]
public class Writer : AiAgent<WriterState>
{
    protected override string Namespace => Consts.OrleansNamespace;
    
    private readonly ILogger<Writer> _logger;

    public Writer([PersistentState("state", "messages")] IPersistentState<AgentState<WriterState>> state, 
    [FromKeyedServices("AzureOpenAI")] Kernel kernel, 
    [FromKeyedServices("QdrantMemory")] ISemanticTextMemory memory, ILogger<Writer> logger) 
    : base(state, memory, kernel)
    {
        _logger = logger;
    }

    public async override Task HandleEvent(Event item)
    {
        switch (item.Type)
        {
            case nameof(EventTypes.UserChatInput):                
                {
                    var userMessage = item.Data["userMessage"]; 
                    _logger.LogInformation($"[{nameof(Writer)}] Event {nameof(EventTypes.UserChatInput)}. UserMessage: {userMessage}");
                
                    var context = new KernelArguments { ["input"] = AppendChatHistory(userMessage) };
                    string newArticle = await CallFunction(WriterPrompts.Write, context);
                    await PublishEvent(Consts.OrleansNamespace, this.GetPrimaryKeyString(), new Event
                    {
                        Type = nameof(EventTypes.ArticleCreated),
                        Data = new Dictionary<string, string> {
                                        { "UserId", item.Data["UserId"] },
                                        { "article", newArticle },
                                    }
                    });
                    break;   
                }
                
            default:
                break;
        }
    }
}