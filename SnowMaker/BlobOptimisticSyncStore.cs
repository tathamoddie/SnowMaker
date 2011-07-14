﻿using System.Text;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using System.Net;

namespace SnowMaker
{
    /// <summary>
    /// Stores a single string value in Blob storage and provides an easy way to update 
    /// the value using Optimistic Concurrency.
    /// </summary>
    public class BlobOptimisticSyncStore : IOptimisticSyncStore
    {
        readonly CloudBlob blobReference;

        public BlobOptimisticSyncStore(CloudStorageAccount account, string container, string address)
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
