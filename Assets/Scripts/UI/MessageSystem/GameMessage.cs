using System;

public struct GameMessage
{
    public string Text;
    public MessageSpeaker Speaker;

    public GameMessage(string text, MessageSpeaker speaker)
    {
        Text = text;
        Speaker = speaker;
    }

    public override string ToString()
    {
        return Text;
    }
}
