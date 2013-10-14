using System.Collections.Generic;
using System.Net;
using System.Text;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.IO;
using System;

namespace SnowMaker
{
    public class BlobOptimisticDataStore : IOptimisticDataStore
    {
        const string SeedValue = "1";

        readonly CloudBlobContainer blobContainer;

        readonly IDictionary<string, ICloudBlob> blobReferences;
        readonly object blobReferencesLock = new object();

        public BlobOptimisticDataStore(CloudStorageAccount account, string containerName)
        {
            var blobClient = account.CreateCloudBlobClient();
            blobContainer = blobClient.GetContainerReference(containerName.ToLower());
            blobContainer.CreateIfNotExists();

            blobReferences = new Dictionary<string, ICloudBlob>();
        }
        
        public long GetNextBatch(string blockName, int batchSize)
        {
            if (batchSize <= 0)
                throw new ArgumentOutOfRangeException("batchSize");

            long id;
            var blobReference = GetBlobReference(blockName);
            using (var stream = new MemoryStream())
            {
                blobReference.DownloadToStream(stream);
                if (!Int64.TryParse(Encoding.UTF8.GetString(stream.ToArray()), out id))
                    throw new Exception(String.Format("The id seed returned from the blob for blockName '{0}' was corrupt, and could not be parsed as a long. The data returned was: {1}", blockName, Encoding.UTF8.GetString(stream.ToArray())));
                if (id <= 0)
                    throw new Exception(String.Format("The id seed returned from the blob for blockName '{0}' was {1}", blockName, id));
            }

            try
            {
                UploadText(
                    blobReference,
                    (id + batchSize).ToString(),
                    AccessCondition.GenerateIfMatchCondition(blobReference.Properties.ETag));
            }
            catch (StorageException exc)
            {
                if (exc.RequestInformation.HttpStatusCode == (int)HttpStatusCode.PreconditionFailed)
                    return -1;
            }

            return id;
        }

        ICloudBlob GetBlobReference(string blockName)
        {
            return blobReferences.GetValue(
                blockName,
                blobReferencesLock,
                () => InitializeBlobReference(blockName));
        }

        private ICloudBlob InitializeBlobReference(string blockName)
        {
            var blobReference = blobContainer.GetBlockBlobReference(blockName);

            if (blobReference.Exists())
                return blobReference;

            try
            {
                UploadText(blobReference, SeedValue, AccessCondition.GenerateIfNoneMatchCondition("*"));
            }
            catch (StorageException uploadException)
            {
                if (uploadException.RequestInformation.HttpStatusCode != (int)HttpStatusCode.Conflict)
                    throw;
            }

            return blobReference;
        }

        void UploadText(ICloudBlob blob, string text, AccessCondition accessCondition)
        {
            blob.Properties.ContentEncoding = "UTF-8";
            blob.Properties.ContentType = "text/plain";
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                blob.UploadFromStream(stream, accessCondition);
            }
        }
    }
}
