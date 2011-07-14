using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using System.Net;

namespace Evolve.WindowsAzure
{
    /// <summary>
    /// Stores a single string value in Blob storage and provides an easy way to update 
    /// the value using Optimistic Concurrency.
    /// </summary>
    public class BlobOptimisticSyncStore : IOptimisticSyncStore
    {
        private readonly CloudBlob _blobReference;

        public BlobOptimisticSyncStore(CloudStorageAccount account, string container, string address)
        {
            var blobClient = account.CreateCloudBlobClient();
            var blobContainer = blobClient.GetContainerReference(container.ToLower());
            _blobReference = blobContainer.GetBlobReference(address);
        }

        public string GetData()
        {
            string data = _blobReference.DownloadText();
            return data;
        }

        public bool TryOptimisticWrite(string data)
        {
            try
            {
                _blobReference.UploadText(
                    data,
                    Encoding.Default,
                    new BlobRequestOptions { 
                        AccessCondition = AccessCondition.IfMatch(_blobReference.Properties.ETag) });
            }
            catch (StorageClientException exc)
            {
                if (exc.StatusCode == HttpStatusCode.PreconditionFailed)
                {
                    return false;
                }
                else
                {
                    throw;
                }
            }
            return true;
        }
    }
}
