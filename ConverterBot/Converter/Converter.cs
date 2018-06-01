using HtmlAgilityPack;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using UnidecodeSharpFork;

namespace ConverterBot
{
    public class Converter
    {
        public const string APIKey = "4ab3610a7b944c7efe63e2fa3eb39ab4";
        public const string Url = "https://api2.online-convert.com/jobs";
        private const int CheckJobStatusInterval = 10000;
        private Dictionary<string, List<string>> allTypes = new Dictionary<string, List<string>>();
        private Dictionary<string, Option[]> options = new Dictionary<string, Option[]>();
        public Converter()
        {
            ParseHtml();
            ParseOptions();
        }

        public ConvertResult Convert(string targetType, string targetFormat, string filename, Dictionary<string, string> selectedOptions, int maxSize = int.MaxValue)
        {
            try
            {
                return ProcessRequest(targetType, targetFormat, filename, selectedOptions, maxSize);
            }
            catch (Exception e)
            {
                return new ConvertResult(filename, error: true);
            }
            finally
            {
                File.Delete(filename);
            }
        }

        private ConvertResult ProcessRequest(string targetType, string targetFormat, string filename, Dictionary<string, string> selectedOptions,
            int maxSize)
        {
            var client = new RestClient(Url);
            var response = client.Execute(CreateFirstRequest(targetType, targetFormat, selectedOptions));
            var answer = JsonValue.Parse(response.Content);
            client = new RestClient(GetUrlForUpload(answer));
            var convertRequest = CreateConvertRequest(filename);
            var convertRequestResult = JsonValue.Parse(client.Execute(convertRequest).Content);
            var jobId = convertRequestResult["id"]["job"];
            var checkStatusClient = new RestClient(Url + "/" + (string) jobId);
            var result = WaitEndOfConversion(checkStatusClient, out var checkStatusResponse);
            if (!result) return new ConvertResult(filename, error: true);
            var downloadUri = checkStatusResponse["output"][0]["uri"];
            var size = checkStatusResponse["output"][0]["size"];
            var newFilename = ((string) downloadUri).Split('/').Last();
            if (int.Parse((string) size) / 1024 / 1024 >= maxSize)
                return new ConvertResult(newFilename, downloadUri: (string) downloadUri);
            var downloadClient = new RestClient((string) downloadUri);
            var file = downloadClient.Execute(CheckJobStatusRequest());
            return new ConvertResult(newFilename, file.RawBytes);
        }

        private bool WaitEndOfConversion(RestClient checkStatusClient, out JsonValue checkStatusResponse)
        {
            checkStatusResponse = JsonValue.Parse("null");
            var flag = true;
            while (flag)
            {
                checkStatusResponse = JsonValue.Parse(checkStatusClient.Execute(CheckJobStatusRequest()).Content);
                flag = checkStatusResponse.ContainsKey("status") &&
                       (string)checkStatusResponse["status"]["code"] == "processing";
                Thread.Sleep(CheckJobStatusInterval);
                if (checkStatusResponse.ContainsKey("errors") && checkStatusResponse["errors"].Count != 0)
                    return false;
            }
            return true;
        }

        private static string GetUrlForUpload(JsonValue answer)
            => ((string)answer["server"]).TrimEnd('1') + "2/upload-base64/" + (string)answer["id"];


        public RestRequest CreateFirstRequest(string targetType, string targetFormat, Dictionary<string, string> selectedOptions)
        {
            var request = CreateBasePartOfRequest(Method.POST, "api2.online-convert.com");
            request.AddHeader("Content-Type", "application/json");
            var options = ConvertSelectedOptionsToString(selectedOptions);
            var requestBody = "{\"conversion\":[{\"target\":\"" + targetFormat + "\", \"options\": {" + options + "}  }]}";
            request.AddParameter("application/json", JsonValue.Parse(requestBody), ParameterType.RequestBody);
            return request;
        }

        public RestRequest CreateConvertRequest(string filename)
        {
            var request = CreateBasePartOfRequest(Method.POST, "www13.online-convert.com");
            var format = filename.Split('.').Last();
            var contentType = GetTypeByFormat(format) + "/" + format;
            var file = ConverFileToBase64andDel(filename);
            var correctName = filename.Replace(' ', '_').Unidecode().Split('.')[0];
            var requestString = "{\r\n  \"content\": \"data:" + contentType + ";base64," + file + "\",\r\n  \"filename\": \"" + correctName + "\"\r\n}";
            request.AddParameter("application/json", JsonValue.Parse(requestString), ParameterType.RequestBody);
            return request;
        }

        private string ConverFileToBase64andDel(string filename)
        {
            var bytes = File.ReadAllBytes(filename);
            var file = System.Convert.ToBase64String(bytes);
            File.Delete(filename);
            return file;
        }

        private static RestRequest CreateBasePartOfRequest(Method method, string host = null)
        {
            var request = new RestRequest(method);
            request.AddHeader("x-oc-api-key", APIKey);
            request.AddHeader("Cache-Control", "no-cache");
            if (host != null) request.AddHeader("Host", host);
            return request;
        }

        public static RestRequest CheckJobStatusRequest() => CreateBasePartOfRequest(Method.GET);


        private void ParseHtml()
        {
            var http = new HttpClient();
            var response = http.GetByteArrayAsync("https://www.online-convert.com/").Result;
            var source = WebUtility.HtmlDecode(Encoding.GetEncoding("utf-8").GetString(response, 0, response.Length - 1));
            var result = new HtmlDocument();
            result.LoadHtml(source);
            var toftitle = result.DocumentNode
                .Descendants("div")
                .Where(x => x.Attributes.Contains("class") && IsConverterName(x))
                .ToList();
            var types = toftitle.Select(x => x.Descendants("h2").First().InnerText.Split(' ')[0].ToLower()).ToArray();
            var formats = toftitle.Select(x => x.Descendants("option").Select(y => y.Attributes["value"]).Skip(1).ToList()).ToList();
            FillTypesAndFormats(types, formats);
        }

        private void FillTypesAndFormats(string[] types, List<List<HtmlAttribute>> formats)
        {
            for (var i = 0; i < types.Length; i++)
            {
                if (types[i] == "webservice") continue;
                var fmts = new List<string>();
                for (var j = 0; j < formats[i].Count; j++)
                    fmts.Add(formats[i][j].Value.Split('-').Last());
                allTypes.Add(types[i], fmts);
            }
        }

        private bool IsConverterName(HtmlNode x)
        {
            return x.Attributes["class"].Value.StartsWith("select_converter1")
                   || x.Attributes["class"].Value.StartsWith("select_converter2");
        }

        private void ParseOptions()
        {
            var url = "https://api2.online-convert.com/conversions?target=";
            var http = new HttpClient();
            foreach (var type in allTypes.Values)
                foreach (var format in type)
                    FillOptionsInfo(http, url, format);
        }

        private void FillOptionsInfo(HttpClient http, string url, string format)
        {
            try
            {
                var response = http.GetByteArrayAsync(url + format).Result;
                var sr = Encoding.GetEncoding("utf-8").GetString(response, 0, response.Length - 1);
                var source = JsonValue.Parse(sr.Substring(1).TrimEnd(']'))["options"];
                var options = new List<Option>();
                foreach (var opt in source)
                {
                    if ((string) opt.Value["type"] != "boolean" && !opt.Value.ContainsKey("enum")) continue;
                    var discription = GetOptionDescription(opt);
                    var varians = GeOptionVariants(opt);
                    options.Add(new Option(opt.Key, discription, varians));
                }

                this.options.Add(format, options.ToArray());
            }
            catch (Exception e)
            {
                if (!options.ContainsKey(format))
                    options.Add(format, new Option[0]);
            }
        }

        private static string GetOptionDescription(KeyValuePair<string, JsonValue> opt)
        {
            return opt.Value.ContainsKey("description") ? (string)opt.Value["description"] : null;
        }

        private static string[] GeOptionVariants(KeyValuePair<string, JsonValue> opt)
        {
            return opt.Value.ContainsKey("enum") ?opt.Value["enum"]
                .Select(x => (string)x.Value)
                .ToArray() : null;
        }

        private string ConvertSelectedOptionsToString(Dictionary<string, string> selectedOptinons)
        {
            var res = "";
            foreach (var opt in selectedOptinons)
                res += opt.Value == "true" ? $"\"{opt.Key}\":{opt.Value}," : $"\"{opt.Key}\":\"{opt.Value}\",";
            return res.TrimEnd(',');
        }

        public string GetTypeByFormat(string format)
        {
            var res = "document";
            foreach (var x in allTypes)
                if (x.Value.Contains(format))
                {
                    res = x.Key;
                    break;
                }
            return res;
        }

        public List<string> GetAllTypes() => allTypes.Select(x => x.Key).ToList();
        public List<string> GetFormatsByType(string type) => allTypes[type];
        public string[] GetOptionVariants(string format, string option) => options[format].First(x => x.Name == option).Variation;
        public string GetOptionDescription(string format, string option) => options[format].First(x => x.Name == option).Description;
        public IEnumerable<string> GetAllOptions(string format) => options[format].Select(x => x.Name);
    }

    public class Option
    {
        public string Name;
        public string[] Variation;
        public string Description;

        public Option(string name, string description = null, string[] variation = null)
        {
            Name = name;
            Variation = variation;
            Description = description;
        }
    }
}
