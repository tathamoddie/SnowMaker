using System;
using System.IO;
using System.Text;

namespace SnowMaker
{
    public class FileOptimisticDataStore : IOptimisticDataStore
    {
        public static readonly Encoding Encoding = Encoding.Default;
        public const long SeedValue = 1;

        readonly string directoryPath;

        public FileOptimisticDataStore(string directoryPath)
        {
            this.directoryPath = directoryPath;
        }

        public long GetNextBatch(string blockName, int batchSize)
        {
            if (batchSize <= 0)
                throw new ArgumentOutOfRangeException("batchSize");

            var blockPath = Path.Combine(directoryPath, string.Format("{0}.txt", blockName));

            try
            {
                using (FileStream stream = File.Open(blockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    if (stream.Length == 0)
                    {
                        // a new file was created
                        using (StreamWriter writer = new StreamWriter(stream, Encoding))
                            writer.Write((SeedValue + batchSize).ToString());
                        return SeedValue;
                    }

                    // read the next available id
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

                    long id;
                    if (!Int64.TryParse(str.ToString(), out id))
                        throw new Exception(String.Format("The id seed returned from the file for blockName '{0}' was corrupt, and could not be parsed as a long. The data returned was: {1}", blockName, str.ToString()));
                    if (id <= 0)
                        throw new Exception(String.Format("The id seed returned from the file for blockName '{0}' was {1}", blockName, id));

                    // mark the next batch as taken
                    stream.Position = 0;
                    stream.SetLength(Encoding.GetByteCount((id + batchSize).ToString()));
                    using (StreamWriter writer = new StreamWriter(stream, Encoding))
                        writer.Write((id + batchSize).ToString());

                    return id;
                }
            }
            catch (IOException)
            {
                return -1;
            }
        }
    }
}
