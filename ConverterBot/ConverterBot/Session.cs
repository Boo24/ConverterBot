using System.Collections.Generic;

namespace ConverterBot
{
    public enum SessionState
    {
        GetTargetType,
        GetTargetFormat,
        SelectConvertOrOptions,
        GetOptions,
        SelectVariant
    }
    public class Session
    {
        public string Filename;
        private string targetType;
        public string currentSelectedOption;
        public string TargetType
        {
            get => targetType;
            set
            {
                CurrentState = SessionState.GetTargetFormat;
                targetType = value;
            }
        }

        private string targetFormat;

        public string TargetFormat
        {
            get => targetFormat;
            set
            {
                CurrentState = SessionState.SelectConvertOrOptions;
                targetFormat = value;
            }
        }
        public Dictionary<string, string> Options = new Dictionary<string, string>();

        public void RemoveCurrentSelectedOption()
        {
            if (Options.ContainsKey(currentSelectedOption))
                Options.Remove(currentSelectedOption);
            currentSelectedOption = null;
            CurrentState = SessionState.GetOptions;
        }

        public void AddOption(string value)
        {
            if (!Options.ContainsKey(currentSelectedOption))
                Options.Add(currentSelectedOption, value);
            else
                Options[currentSelectedOption] = value;
            currentSelectedOption = null;
            CurrentState = SessionState.GetOptions;

        }

        public void AddCurrentSelectedOption(string option)
        {
            currentSelectedOption = option;
            CurrentState = SessionState.SelectVariant;
        }

        public SessionState CurrentState { get; set; }
        public Session(string filename)
        {
            Filename = filename;
            CurrentState = SessionState.GetTargetType;
        }



    }
}
