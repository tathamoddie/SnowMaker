using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace SnowMaker
{
    public class BlobOptimisticDataStore : IOptimisticDataStore
    {
        const string SeedValue = "1";

        readonly CloudBlobContainer blobContainer;

        readonly IDictionary<string, CloudBlockBlob> blobReferences;
        readonly object blobReferencesLock = new object();

        public BlobOptimisticDataStore(CloudStorageAccount account, string containerName)
        {
            var blobClient = account.CreateCloudBlobClient();
            blobContainer = blobClient.GetContainerReference(containerName.ToLower());
            blobContainer.CreateIfNotExists();

            blobReferences = new Dictionary<string, CloudBlockBlob>();
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
                blobReference.UploadText(data, AccessCondition.GenerateIfMatchCondition(blobReference.Properties.ETag));
            }
            catch (StorageException exc)
            {
                if (exc.RequestInformation.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
                    return false;

                throw;
            }
            return true;
        }

        CloudBlockBlob GetBlobReference(string blockName)
        {
            return blobReferences.GetValue(
                blockName,
                blobReferencesLock,
                () => InitializeBlobReference(blockName));
        }

        private CloudBlockBlob InitializeBlobReference(string blockName)
        {
            var blobReference = blobContainer.GetBlockBlobReference(blockName);

            try
            {
                blobReference.DownloadText();
            }
            catch (StorageException downloadException)
            {
                if (downloadException.RequestInformation.HttpStatusCode != (int)HttpStatusCode.NotFound)
                    throw;

                try
                {
                    blobReference.UploadText(SeedValue, AccessCondition.GenerateIfNoneMatchCondition("*"));
                }
                catch (StorageException uploadException)
                {
                    if (uploadException.RequestInformation.HttpStatusCode != (int)HttpStatusCode.Conflict)
                        throw;
                }
            }

            return blobReference;
        }
    }
}
