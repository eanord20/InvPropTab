using System;
using System.IO;

namespace Inv_Property_Tab.Diagnostics
{
    public sealed class AppLogger
    {
        private const long MaximumLength = 1024 * 1024;
        private static readonly object Sync = new object();
        private readonly string _path;

        public AppLogger()
            : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "InvPropTab"))
        {
        }

        public AppLogger(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentException("Log directory is required.", nameof(directory));
            _path = Path.Combine(directory, "invproptab.log");
        }

        public void Error(string operation, Exception exception)
        {
            Write("ERROR", operation + ": " + exception.Message);
        }

        public void Warning(string message)
        {
            Write("WARN", message);
        }

        public void Info(string message)
        {
            Write("INFO", message);
        }

        private void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    var directory = Path.GetDirectoryName(_path)!;
                    Directory.CreateDirectory(directory);
                    if (File.Exists(_path) && new FileInfo(_path).Length >= MaximumLength)
                        File.WriteAllText(_path, string.Empty);
                    File.AppendAllText(_path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }
}
