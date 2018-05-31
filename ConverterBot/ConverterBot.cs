using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
namespace ConverterBot
{
    public enum MessType
    {
        Text,
        File
    }
    public class ConverterBot : IBot
    {
        private ConcurrentDictionary<long, Session> currentSessions = new ConcurrentDictionary<long, Session>();
        private Converter converter;
        private string selectOptionsMessage = "Select options or press CONVERT for conversion:";
        private string selectTypeMess = "Select type of resulting file:";
        private string selectFormatMess = "Select format of resulting file:";

        private string abortButtonMess = "Your session has been successfully completed! Send a new file to get started.";
        private string abortMess = "ABORT SESSION";
        private string backButtonName = "BACK";
        private string converButtonName = "Convert ✔";
        private string okButtonName = "✔";
        private string cancelButtonNane = "✖";
        private string startMessage = "To the start of conversion, send a file!";
        private string resultMessageForBigFile = "Unfortunately, the resulting file is too " +
                                                 "large to download it here :( But you can download it here: ";
        private List<string> okOrCancelButtonsName;
        private List<string> defaultButtons;
        private Dictionary<SessionState, Func<string, long, IResponse>> stateHandlers;
        private Dictionary<string, Func<long, IResponse>> defaultCommandHandlers;
        public ConverterBot(Converter converter)
        {
            this.converter = converter;
            okOrCancelButtonsName = new List<string> { okButtonName, cancelButtonNane };
            defaultButtons = new List<string> { backButtonName, abortMess };
            stateHandlers = new Dictionary<SessionState, Func<string, long, IResponse>>() {

                {SessionState.GetTargetType, HandleGetTargetTypeState},
                {SessionState.GetTargetFormat, HandleGetTargetFormatState},
                {SessionState.SelectConvertOrOptions, HandleSelectConvertOrOptions},
                {SessionState.GetOptions,  HandleGetOptionState },
                {SessionState.SelectVariant,  HandleSelectVariantState}
            };
            defaultCommandHandlers = new Dictionary<string, Func<long, IResponse>>
            {
                {abortMess, HandleCancelButton},
                {backButtonName, HandleBackCommans},
                {converButtonName, HandleConvertCommand}
            };
        }

        public IResponse HandleCommand(string command, MessType messType, long chatId)
        {
            if (messType == MessType.File)
            {
                currentSessions.AddOrUpdate(chatId, new Session(command), (l, session) => new Session(command));
                return GetSelectTypeMenu();
            }
            if (command == "/help" || command == "/start" || !currentSessions.ContainsKey(chatId))
                return new MessageResponse(startMessage);
            if (defaultCommandHandlers.ContainsKey(command))
                return defaultCommandHandlers[command](chatId);
            return stateHandlers[currentSessions[chatId].CurrentState](command, chatId);
        }

        private IResponse HandleSelectConvertOrOptions(string command, long chatId)
        {

            currentSessions[chatId].CurrentState = SessionState.GetOptions;
            return GetOptionsMenuByFormat(currentSessions[chatId].TargetFormat);
        }

        private IResponse HandleSelectVariantState(string command, long chatId)
        {
            var curTargetFormat = currentSessions[chatId].TargetFormat;
            var curSelectedOption = currentSessions[chatId].currentSelectedOption;
            if (command == okButtonName && !OptionHasVariants(curSelectedOption, curTargetFormat))
            {
                command = "true";
                currentSessions[chatId].AddOption(command);
                return GetOptionsMenuByFormat(curTargetFormat);
            }

            if (command == cancelButtonNane)
            {
                currentSessions[chatId].RemoveCurrentSelectedOption();
                return GetOptionsMenuByFormat(curTargetFormat);
            }

            if (!OptionHasVariants(curSelectedOption, curTargetFormat))
                return GetOkOrCancelMenu(curTargetFormat, curSelectedOption);

            if (!IsCorrectVariantOfOption(curSelectedOption, command, curTargetFormat))
                return GetOptionVariants(curTargetFormat, curSelectedOption);

            currentSessions[chatId].AddOption(command);
            return GetOptionsMenuByFormat(curTargetFormat);
        }

        private IResponse HandleGetOptionState(string command, long chatId)
        {
            var curTargetFormat = currentSessions[chatId].TargetFormat;
            if (!converter.GetAllOptions(curTargetFormat).Contains(command))
                return GetOptionsMenuByFormat(curTargetFormat);

            if (!OptionHasVariants(command, curTargetFormat))
            {
                currentSessions[chatId].AddCurrentSelectedOption(command);
                return GetOkOrCancelMenu(curTargetFormat, command);
            }

            currentSessions[chatId].AddCurrentSelectedOption(command);
            return GetOptionVariants(curTargetFormat, command);
        }

        private IResponse HandleGetTargetFormatState(string command, long chatId)
        {
            var curTargetType = currentSessions[chatId].TargetType;
            if (!converter.GetFormatsByType(curTargetType).Contains(command))
                return GetSelectFormatMenu(curTargetType);
            currentSessions[chatId].TargetFormat = command;
            return GetSelectConvertOrOptionsMenu();
        }

        private IResponse HandleConvertCommand(long chatId)
        {
            var result = converter.Convert(currentSessions[chatId].TargetType, currentSessions[chatId].TargetFormat,
                currentSessions[chatId].Filename, currentSessions[chatId].Options, 30);
            currentSessions.TryRemove(chatId, out _);
            if (result.Error)
                return new MessageResponse("Unfortunately, the conversion of the file failed.");
            if (result.File is null)
                return new MessageResponse(resultMessageForBigFile + result.DownloadUri);
            return new FileResponse(result.File, result.Name);
        }


        private IResponse HandleCancelButton(long chatId)
        {
            currentSessions.TryRemove(chatId, out _);
            return new MessageResponse(abortButtonMess);
        }

        private IResponse HandleGetTargetTypeState(string command, long chatId)
        {
            if (!converter.GetAllTypes().Contains(command))
                return GetSelectTypeMenu();
            currentSessions[chatId].TargetType = command;
            return GetSelectFormatMenu(command);
        }

        private IResponse HandleBackCommans(long chatId)
        {
            switch (currentSessions[chatId].CurrentState)
            {
                case SessionState.GetTargetFormat:
                    currentSessions[chatId].CurrentState = SessionState.GetTargetType;
                    return GetSelectTypeMenu();
                case SessionState.SelectConvertOrOptions:
                    currentSessions[chatId].CurrentState = SessionState.GetTargetFormat;
                    currentSessions[chatId].Options.Clear();
                    return GetSelectFormatMenu(currentSessions[chatId].TargetType);
                case SessionState.GetOptions:
                    currentSessions[chatId].CurrentState = SessionState.SelectConvertOrOptions;
                    return GetSelectConvertOrOptionsMenu();
                case SessionState.SelectVariant:
                    currentSessions[chatId].RemoveCurrentSelectedOption();
                    currentSessions[chatId].CurrentState = SessionState.GetOptions;
                    return GetOptionsMenuByFormat(currentSessions[chatId].TargetFormat);
            }

            throw new NotImplementedException();

        }
        public IResponse GetOptionVariants(string targetFormat, string optionName)
        {
            var opt = converter.GetOptionVariants(targetFormat, optionName).ToList();
            return new ButtonResponse(opt.Count, "Select one of the options:", opt, defaultButtons);
        }
        private IResponse GetOkOrCancelMenu(string curTargetFormat, string option) =>
            new ButtonResponse(2, GetOptionDescription(curTargetFormat, option), okOrCancelButtonsName);
        private string GetOptionDescription(string curTargetFormat, string option) =>
            converter.GetOptionDescription(curTargetFormat, option) ?? $"Apply {option}?";

        private List<string> GetOptionsByFormat(string format) => new[] { converButtonName }.Concat(converter.GetAllOptions(format)).ToList();
        private bool OptionHasVariants(string option, string format) => !(converter.GetOptionVariants(format, option) is null);
        private bool IsCorrectVariantOfOption(string opt, string var, string format) => converter.GetOptionVariants(format, opt).Contains(var);
        private IResponse GetSelectTypeMenu() => new ButtonResponse(3, selectTypeMess, converter.GetAllTypes(), new List<string> { abortMess });
        private IResponse GetSelectFormatMenu(string type) => new ButtonResponse(4, selectFormatMess, converter.GetFormatsByType(type), defaultButtons);

        private IResponse GetSelectConvertOrOptionsMenu() => new ButtonResponse(2, selectOptionsMessage, new List<string> { converButtonName, "OPTIONS" }, defaultButtons);
        private IResponse GetOptionsMenuByFormat(string format) => new ButtonResponse(5, selectOptionsMessage, GetOptionsByFormat(format), defaultButtons);


    }
}
