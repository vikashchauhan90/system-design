using System;
using System.Collections.Generic;

namespace DistributedSystem.Outbox;

public sealed record OutboxMessage(string Id, string EventType, string Payload);

public sealed class OutboxStore
{
    private readonly List<OutboxMessage> _messages = [];

    public void Add(OutboxMessage message)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        _messages.Add(message);
    }

    public IReadOnlyList<OutboxMessage> GetPendingMessages()
    {
        return _messages.AsReadOnly();
    }

    public void MarkPublished(string messageId)
    {
        _messages.RemoveAll(message => message.Id == messageId);
    }
}

public sealed class TransactionOutbox
{
    private readonly OutboxStore _store;

    public TransactionOutbox(OutboxStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public void RecordEvent(string eventType, string payload)
    {
        var message = new OutboxMessage(Guid.NewGuid().ToString("N"), eventType, payload);
        _store.Add(message);
        Console.WriteLine($"Recorded outbox message: {message.Id} ({message.EventType})");
    }

    public void PublishPendingMessages(Action<OutboxMessage> publish)
    {
        if (publish is null)
        {
            throw new ArgumentNullException(nameof(publish));
        }

        foreach (var message in _store.GetPendingMessages())
        {
            publish(message);
            _store.MarkPublished(message.Id);
        }
    }
}
