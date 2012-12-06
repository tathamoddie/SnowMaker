using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace SnowMaker
{
    // See http://msdn.microsoft.com/en-us/library/windowsazure/jj721952.aspx
    //     http://blogs.msdn.com/b/windowsazurestorage/archive/2012/10/29/windows-azure-storage-client-library-2-0-breaking-changes-amp-migration-guide.aspx
    public static class CloudBlockBlobExtensions
    {
        public static void UploadText(this CloudBlockBlob blockBlob, string content, AccessCondition accessCondition = null)
        {
            using (var stream = blockBlob.OpenWrite(accessCondition))
            {
                var encoding = new UTF8Encoding();
                var buffer = encoding.GetBytes(content);

                stream.Write(buffer, 0, buffer.Length);
                stream.Flush();
                stream.Close();
            }
        }

        public static string DownloadText(this CloudBlockBlob blockBlob)
        {
            using (var stream = blockBlob.OpenRead())
            {
                var buffer = new byte[256];
                int read = stream.Read(buffer, 0, buffer.Length);
                stream.Close();

                var encoding = new UTF8Encoding();
                return encoding.GetString(buffer, 0, read);
            }
        }
    }
}
