using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;

namespace SnowMaker
{
    public class BlobOptimisticDataStore : IOptimisticDataStore
    {
        const string SeedValue = "1";

        readonly CloudBlobContainer blobContainer;

        readonly IDictionary<string, CloudBlob> blobReferences;
        readonly object blobReferencesLock = new object();

        public BlobOptimisticDataStore(CloudStorageAccount account, string containerName)
        {
            var blobClient = account.CreateCloudBlobClient();
            blobContainer = blobClient.GetContainerReference(containerName.ToLower());
            blobContainer.CreateIfNotExist();

            blobReferences = new Dictionary<string, CloudBlob>();
        }

        public string GetData(string blockName)
        {
            var blobReference = GetBlobReference(blockName);
            return blobReference.DownloadText();
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

        CloudBlob GetBlobReference(string blockName)
        {
            return blobReferences.GetValue(
                blockName,
                blobReferencesLock,
                () => InitializeBlobReference(blockName));
        }

        private CloudBlob InitializeBlobReference(string blockName)
        {
            var blobReference = blobContainer.GetBlobReference(blockName);

            try
            {
                blobReference.DownloadText();
            }
            catch (StorageClientException downloadException)
            {
                if (downloadException.StatusCode != HttpStatusCode.NotFound)
                    throw;

                try
                {
                    blobReference.UploadText(
                        SeedValue,
                        Encoding.Default,
                        new BlobRequestOptions { AccessCondition = AccessCondition.IfNoneMatch("*") });
                }
                catch (StorageClientException uploadException)
                {
                    if (uploadException.StatusCode != HttpStatusCode.Conflict)
                        throw;
                }
            }

            return blobReference;
        }
    }
}
