using System.Text;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using System.Net;

namespace SnowMaker
{
    public class BlobOptimisticDataStore : IOptimisticDataStore
    {
        readonly CloudBlob blobReference;

        public BlobOptimisticDataStore(CloudStorageAccount account, string container, string address)
        {
            var blobClient = account.CreateCloudBlobClient();
            var blobContainer = blobClient.GetContainerReference(container.ToLower());
            blobReference = blobContainer.GetBlobReference(address);
        }

        public string GetData()
        {
            return blobReference.DownloadText();
        }

        public bool TryOptimisticWrite(string data)
        {
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
