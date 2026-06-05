using AmbientContext.Abstractions;
using AmbientContext.Messaging.Sample;
using Microsoft.Extensions.DependencyInjection;

[assembly: AmbientContext(
    typeof(Guid),
    "ClientId",
    Namespace = "AmbientContext.Messaging.Sample")]
[assembly: AmbientContext(
    typeof(string),
    "MessageId",
    Namespace = "AmbientContext.Messaging.Sample")]
[assembly: AmbientContextRegistration("AddMessagingSampleAmbientContexts")]

var services = new ServiceCollection();

services.AddMessagingSampleAmbientContexts();
services.AddSingleton<MessageHandler>();
services.AddSingleton<MessageProcessor>();

await using var provider = services.BuildServiceProvider();
var processor = provider.GetRequiredService<MessageProcessor>();
var messages = new[]
{
    new MessageEnvelope(Guid.NewGuid(), "message-1", "First"),
    new MessageEnvelope(Guid.NewGuid(), "message-2", "Second")
};

await Task.WhenAll(messages.Select(message =>
    processor.ProcessAsync(message, CancellationToken.None)));

internal sealed record MessageEnvelope(
    Guid ClientId,
    string MessageId,
    string Body);

internal sealed class MessageProcessor
{
    private readonly IClientIdContext _clientIdContext;
    private readonly IMessageIdContext _messageIdContext;
    private readonly MessageHandler _handler;

    public MessageProcessor(
        IClientIdContext clientIdContext,
        IMessageIdContext messageIdContext,
        MessageHandler handler)
    {
        _clientIdContext = clientIdContext;
        _messageIdContext = messageIdContext;
        _handler = handler;
    }

    public Task ProcessAsync(
        MessageEnvelope message,
        CancellationToken cancellationToken)
    {
        return _clientIdContext.ExecuteAsClientIdAsync(
            message.ClientId,
            outerCancellationToken =>
                _messageIdContext.ExecuteAsMessageIdAsync(
                    message.MessageId,
                    innerCancellationToken =>
                        _handler.HandleAsync(message, innerCancellationToken),
                    outerCancellationToken),
            cancellationToken);
    }
}

internal sealed class MessageHandler
{
    private readonly IClientIdAccessor _clientIdAccessor;
    private readonly IMessageIdAccessor _messageIdAccessor;

    public MessageHandler(
        IClientIdAccessor clientIdAccessor,
        IMessageIdAccessor messageIdAccessor)
    {
        _clientIdAccessor = clientIdAccessor;
        _messageIdAccessor = messageIdAccessor;
    }

    public async Task HandleAsync(
        MessageEnvelope message,
        CancellationToken cancellationToken)
    {
        await Task.Delay(10, cancellationToken);

        Console.WriteLine(
            $"Client {_clientIdAccessor.Current}, " +
            $"message {_messageIdAccessor.Current}: {message.Body}");
    }
}
