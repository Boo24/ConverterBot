namespace ConverterBot
{
    public class MessageResponse : IResponse
    {
        public string Text;

        public MessageResponse(string text) => Text = text;
    }
}
