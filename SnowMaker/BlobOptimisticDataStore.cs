using System.Collections.Generic;
using System.Net;
using System.Text;
using System.IO;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace SnowMaker
{
    public class BlobOptimisticDataStore : IOptimisticDataStore
    {
        const string SeedValue = "1";

        readonly BlobContainerClient blobContainer;

        readonly IDictionary<string, BlobClient> blobReferences;
        readonly object blobReferencesLock = new object();

        public BlobOptimisticDataStore(string storageConnectionString, string containerName)
        {
            blobContainer = new BlobContainerClient(storageConnectionString, containerName);
            blobContainer.CreateIfNotExists();

            blobReferences = new Dictionary<string, BlobClient>();
        }

        public string GetData(string blockName)
        {
            var blobReference = GetBlobReference(blockName);
            using (var stream = new MemoryStream())
            {
                blobReference.DownloadTo(stream);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public bool TryOptimisticWrite(string scopeName, string data)
        {
            var blobReference = GetBlobReference(scopeName);
            try
            {
                var conditions = new BlobRequestConditions
                {
                    IfMatch = blobReference.GetProperties().Value.ETag
                };

                UploadText(blobReference, data, conditions);
            }
            catch (RequestFailedException exc)
            {
                if (exc.Status == (int)HttpStatusCode.PreconditionFailed)
                    return false;

                throw;
            }
            return true;
        }

        BlobClient GetBlobReference(string blockName)
        {
            return blobReferences.GetValue(
                blockName,
                blobReferencesLock,
                () => InitializeBlobReference(blockName));
        }

        private BlobClient InitializeBlobReference(string blockName)
        {
            var blobReference = blobContainer.GetBlobClient(blockName);

            if (blobReference.Exists())
                return blobReference;

            try
            {
                var conditions = new BlobRequestConditions
                {
                    IfNoneMatch = ETag.All
                };

                UploadText(blobReference, SeedValue, conditions);
            }
            catch (RequestFailedException uploadException)
            {
                if (uploadException.Status != (int)HttpStatusCode.Conflict)
                    throw;
            }

            return blobReference;
        }

        void UploadText(BlobClient blob, string text, BlobRequestConditions conditions)
        {
            //var properties = blob.GetProperties();

            var headers = new BlobHttpHeaders
            {
                // Set the MIME ContentType every time the properties 
                // are updated or the field will be cleared
                ContentType = "text/plain",
                ContentLanguage = "en-us",

                // Populate remaining headers with 
                // the pre-existing properties
                //CacheControl = properties.Value.CacheControl,
                //ContentDisposition = properties.Value.ContentDisposition,
                ContentEncoding = "UTF-8", // properties.Value.ContentEncoding,
                //ContentHash = properties.Value.ContentHash
            };

            // Set the blob's properties.
            //blob.SetHttpHeaders(headers);

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(text)))
            {
                // Set the If-Match condition to the original ETag.
                var options = new BlobUploadOptions()
                {
                    Conditions = conditions,
                    HttpHeaders = headers,
                };

                blob.Upload(stream, options);
            }
        }
    }
}
