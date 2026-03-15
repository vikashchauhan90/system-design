# Chat System

```C#
using System;
using System.Collections.Generic;

public class User
{
    public string Name { get; private set; }

    public User(string name)
    {
        Name = name;
    }

    public void ReceiveMessage(Message message)
    {
        Console.WriteLine($"{Name} received: {message.Content} from {message.Sender.Name}");
    }
}

public class Message
{
    public User Sender { get; private set; }
    public string Content { get; private set; }

    public Message(User sender, string content)
    {
        Sender = sender;
        Content = content;
    }
}

public class ChatRoom
{
    private List<Message> _messages = new List<Message>();
    private List<User> _subscribers = new List<User>();

    public void Subscribe(User user)
    {
        _subscribers.Add(user);
    }

    public void PostMessage(User user, string content)
    {
        var message = new Message(user, content);
        _messages.Add(message);

        foreach (var subscriber in _subscribers)
        {
            if (subscriber != user)
            {
                subscriber.ReceiveMessage(message);
            }
        }
    }
}

public class Program
{
    public static void Main(string[] args)
    {
        var alice = new User("Alice");
        var bob = new User("Bob");
        var charlie = new User("Charlie");

        var chatRoom = new ChatRoom();

        chatRoom.Subscribe(alice);
        chatRoom.Subscribe(bob);
        chatRoom.Subscribe(charlie);

        chatRoom.PostMessage(alice, "Hello, everyone!");
        chatRoom.PostMessage(bob, "Hi, Alice!");
    }
}

```