using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;

namespace ConverterBot
{
    public class ButtonResponse : IResponse
    {
        public ReplyKeyboardMarkup rkm;
        public string Message;
        public ButtonResponse(int rowCount, string text, List<string> buttonsName, List<string> additionButtonsName = null)
        {
            Message = text;
            rkm = new ReplyKeyboardMarkup();
            var res = new List<List<KeyboardButton>>();
            for (var i = 0; i < rowCount; i++)
            {
                var newRow = new List<KeyboardButton>();
                for (var j = 0; j < buttonsName.Count / rowCount; j++)
                    newRow.Add(new KeyboardButton(buttonsName[i * buttonsName.Count / rowCount + j]));
                res.Add(newRow);
            }
            if (!(additionButtonsName is null))
                foreach (var name in additionButtonsName)
                {
                    res.Add(new List<KeyboardButton> { name });
                }

            rkm.OneTimeKeyboard = true;
            rkm.Keyboard = res;
        }
    }
}
