namespace ConverterBot
{
    public interface IBot
    {
        IResponse HandleCommand(string command, MessType messageType, long chatId);
    }

    public interface IResponse
    {
    }
}
