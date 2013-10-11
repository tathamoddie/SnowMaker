using System;
using System.IO;
using System.Text;

namespace SnowMaker
{
    public class FileOptimisticDataStore : IOptimisticDataStore
    {
        public static readonly Encoding Encoding = Encoding.Default;
        public const string SeedValue = "1";
        static readonly byte[] SeedBytes = Encoding.GetBytes(SeedValue);

        readonly string directoryPath;
        int maxAccessAttempts = 10;

        public FileOptimisticDataStore(string directoryPath)
        {
            this.directoryPath = directoryPath;
        }

        public int MaxAccessAttempts
        {
            get { return maxAccessAttempts; }
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException("value", maxAccessAttempts, "MaxAccessAttempts must be a positive number.");

                maxAccessAttempts = value;
            }
        }

        public string GetData(string blockName)
        {
            var blockPath = Path.Combine(directoryPath, string.Format("{0}.txt", blockName));

            int retryCount = 0;
            do
            {
                try
                {
                    try
                    {
                        using (TextReader reader = new StreamReader(blockPath, Encoding))
                            return reader.ReadToEnd();
                    }
                    catch (FileNotFoundException)
                    {
                        using (FileStream stream = File.Open(blockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        using (TextWriter writer = new StreamWriter(stream, Encoding))
                            writer.Write(SeedValue);
                        return SeedValue;
                    }
                }
                catch (IOException)
                {
                    if (retryCount++ == maxAccessAttempts)
                        throw;
                }
            }
            while (true);
        }

        public bool TryOptimisticWrite(string blockName, string data, string originalData)
        {
            var blockPath = Path.Combine(directoryPath, string.Format("{0}.txt", blockName));

            if (!File.Exists(blockPath))
                return false;

            int retryCount = 0;
            do
            {
                try
                {
                    using (FileStream stream = File.Open(blockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        if (stream.Length == 0)
                            return false;

                        // can't use StreamReader to read here bc it would call Dispose on the provided stream object when StreamReader is disposed
                        StringBuilder str = new StringBuilder();
                        byte[] buffer = new byte[128];
                        int offset = 0, length;
                        do
                        {
                            length = stream.Read(buffer, offset, buffer.Length);
                            str.Append(Encoding.GetString(buffer, 0, length));
                            offset += length;
                        }
                        while (stream.Position < stream.Length);

                        if (str.ToString().Equals(originalData))
                        {
                            stream.Position = 0;
                            stream.SetLength(Encoding.GetByteCount(data));
                            using (StreamWriter writer = new StreamWriter(stream, Encoding))
                                writer.Write(data);
                            return true;
                        }
                        else
                            return false;
                    }
                }
                catch (IOException)
                {
                    if (retryCount++ == maxAccessAttempts)
                        return false;
                }
            }
            while (true);
        }
    }
}
