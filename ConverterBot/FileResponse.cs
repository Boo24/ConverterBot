namespace ConverterBot
{
    public class FileResponse : IResponse
    {
        public byte[] File;
        public string Filename;
        public string DownloadUri;
        public FileResponse(byte[] file, string filename, string downloadUri = null)
        {
            File = file;
            Filename = filename;
            DownloadUri = downloadUri;
        }
    }
}
