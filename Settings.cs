using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;

namespace HTTPServer
{
    class Settings
    {
        private const string fileKey = "file_";

        private static int _port;
        private static string _folder;
        private static readonly ConcurrentDictionary<string, string> _files = new ConcurrentDictionary<string, string>();
        

        public static void Initialization()
        {
            _port = int.Parse(ConfigurationManager.AppSettings["port"]);
            _folder = ConfigurationManager.AppSettings["folder"];
            if (!IsSite)
            {
                foreach (string key in ConfigurationManager.AppSettings)
                {
                    if (key.StartsWith(fileKey))
                    {
                        string filePath = ConfigurationManager.AppSettings[key];
                        string fileName = Path.GetFileName(filePath);
                        _files.TryAdd(fileName, filePath);
                    }
                }
            }
        }

        public static int Port {  get { return _port; } }

        public static bool IsSite { get { return !string.IsNullOrEmpty(_folder); } }

        public static string Folder {  get { return _folder; } }

        public static ConcurrentDictionary<string, string> Files {  get { return _files; } }
    }
}
