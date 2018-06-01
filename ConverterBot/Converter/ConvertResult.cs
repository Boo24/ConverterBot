namespace ConverterBot
{
    public class ConvertResult
    {
        public byte[] File;
        public string Name;
        public string DownloadUri;
        public bool Error;

        public ConvertResult(string name, byte[] file = null, string downloadUri = null, bool error = false)
        {
            File = file;
            Name = name;
            DownloadUri = downloadUri;
            Error = error;
        }
    }
}
