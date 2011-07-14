using System.Collections.Generic;
using System.Text;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using System.Net;

namespace SnowMaker
{
    public class BlobOptimisticDataStore : IOptimisticDataStore
    {
        readonly CloudBlobContainer blobContainer;

        readonly IDictionary<string, CloudBlob> blobReferences;
        readonly object blobReferencesLock = new object();

        public BlobOptimisticDataStore(CloudStorageAccount account, string container)
        {
            var blobClient = account.CreateCloudBlobClient();
            blobContainer = blobClient.GetContainerReference(container.ToLower());

            blobReferences = new Dictionary<string, CloudBlob>();
        }

        public string GetData(string blockName)
        {
            var blobReference = GetBlobReference(blockName);
            return blobReference.DownloadText();
        }

        CloudBlob GetBlobReference(string blockName)
        {
            CloudBlob blobReference;
            var found = blobReferences.TryGetValue(blockName, out blobReference);
            if (found) return blobReference;

            lock (blobReferencesLock)
            {
                found = blobReferences.TryGetValue(blockName, out blobReference);
                if (found) return blobReference;

                blobReference = blobContainer.GetBlobReference(blockName);

                blobReferences.Add(blockName, blobReference);
            }

            return blobReference;
        }

        public bool TryOptimisticWrite(string scopeName, string data)
        {
            var blobReference = GetBlobReference(scopeName);
            try
            {
                blobReference.UploadText(
                    data,
                    Encoding.Default,
                    new BlobRequestOptions { AccessCondition = AccessCondition.IfMatch(blobReference.Properties.ETag) });
            }
            catch (StorageClientException exc)
            {
                if (exc.StatusCode == HttpStatusCode.PreconditionFailed)
                    return false;

                throw;
            }
            return true;
        }
    }
}
