using System;
using System.IO;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;

namespace ConverterBot
{
    class TelegramHandler
    {
        private static readonly TelegramBotClient TelegramClient = new TelegramBotClient("562561379:AAErrlWZLJc3DZmar2JIxmE1j7PjpYUhfvk");
        private IBot Bot { get; }

        public TelegramHandler(IBot bot)
        {
            TelegramClient.OnMessage += BotOnMessageReceived;
            Bot = bot;
        }
        public  void Run()
        {
            TelegramClient.StartReceiving();
            Console.ReadLine();
            TelegramClient.StopReceiving();
        }
        private async void BotOnMessageReceived(object sender, MessageEventArgs messageEventArgs)

        {

            Console.WriteLine("BotOnMessageReceived");
            var message = messageEventArgs.Message;
            if (message.Type == MessageType.Photo)
            {
                if (CheckFileSize(message.Photo[1].FileSize, message.Chat.Id)) return;

                var fileInfo = await TelegramClient.GetFileAsync(message.Photo[1].FileId);
                using (var saveImageStream = new FileStream(message.Photo[1].FileId + ".jpg", FileMode.Create))
                    await TelegramClient.GetInfoAndDownloadFileAsync(fileInfo.FileId, saveImageStream);

                var response = Bot.HandleCommand(message.Photo[1].FileId + ".jpg", MessType.File, message.Chat.Id);
                Reply(response, message.Chat.Id);
            }

            if (message.Type == MessageType.Audio)
            {
                if (CheckFileSize(message.Audio.FileSize, message.Chat.Id)) return;

                var fileInfo = await TelegramClient.GetFileAsync(message.Audio.FileId);
                using (var saveImageStream = new FileStream(message.Audio.FileId + message.Audio.MimeType.Split('/')[1], FileMode.Create))
                    await TelegramClient.GetInfoAndDownloadFileAsync(fileInfo.FileId, saveImageStream);
                var response = Bot.HandleCommand(message.Audio.FileId + message.Audio.MimeType.Split('/')[1], MessType.File, message.Chat.Id);
                Reply(response, message.Chat.Id);
            }

            if (message.Type == MessageType.Document)
            {
                if (CheckFileSize(message.Document.FileSize, message.Chat.Id)) return;

                var fileInfo = await TelegramClient.GetFileAsync(message.Document.FileId);
                using (var saveImageStream = new FileStream(message.Document.FileName, FileMode.Create))
                    await TelegramClient.GetInfoAndDownloadFileAsync(fileInfo.FileId, saveImageStream);
                var response = Bot.HandleCommand(message.Document.FileName, MessType.File, message.Chat.Id);
                Reply(response, message.Chat.Id);
            }


            if (message.Type == MessageType.Text)
            {
                var response = Bot.HandleCommand(message.Text, MessType.Text, message.Chat.Id);
                Reply(response, message.Chat.Id);

            }
        }

        private bool CheckFileSize(long size, long chtId)
        {
            if (size / 1024 / 1024 > 30)
            {
                Reply(new MessageResponse("Too big file"), chtId);
                return true;
            }

            return false;
        }


        private async void Reply(IResponse response, long chatId)
        {
            switch (response)
            {
                case ButtonResponse buttonResponse:
                    await TelegramClient.SendTextMessageAsync(chatId, buttonResponse.Message,
                        replyMarkup: buttonResponse.rkm);
                    break;
                case MessageResponse messageResponse:
                    await TelegramClient.SendTextMessageAsync(chatId, messageResponse.Text, replyToMessageId: 0);
                    break;
                case FileResponse fileResponse:
                    await TelegramClient.SendDocumentAsync(chatId,
                        new InputOnlineFile(new MemoryStream(fileResponse.File), fileResponse.Filename));
                    break;
            }
        }
    }
}
