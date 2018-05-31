
namespace ConverterBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var th = new TelegramHandler(new ConverterBot(new Converter()));
            th.Run();
        }
    }
}
