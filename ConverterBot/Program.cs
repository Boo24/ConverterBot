
namespace ConverterBot
{
    class Program
    {
        static void Main(string[] args)
        {
            var converterBot = new ConverterBot(new Converter());
            var th = new TelegramHandler(converterBot);
            th.Run();
        }
    }
}
