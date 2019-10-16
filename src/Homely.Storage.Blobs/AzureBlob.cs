using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Homely.Storage.Blobs
{
    public class AzureBlob : IBlob
    {
        private readonly ILogger<AzureBlob> _logger;
        private readonly string _connectionString;
        private readonly Lazy<Task<CloudBlobContainer>> _container;

        public AzureBlob(string connectionString,
                         string containerName,
                         ILogger<AzureBlob> logger) : this(connectionString, containerName, null, logger)
        {
        }

        public AzureBlob(string connectionString,
                         string containerName, 
                         BlobContainerPermissions permissions,
                         ILogger<AzureBlob> logger)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ArgumentException(nameof(connectionString));
            }

            if (string.IsNullOrWhiteSpace(containerName))
            {
                throw new ArgumentException(nameof(containerName));
            }

            _connectionString = connectionString;
            Name = containerName;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _container = new Lazy<Task<CloudBlobContainer>>(() => CreateCloudBlobContainer(permissions));
        }

        /// <inheritdoc />
        public string Name { get; }

        private Task<CloudBlobContainer> Container
        {
            get { return _container.Value; }
        }

        /// <inheritdoc />
        public async Task<bool> GetAsync(string blobId,
                                         Stream stream,
                                         CancellationToken cancellationToken = default)
        {
            await GetBlobDataAsync(blobId, stream, cancellationToken);

            return stream.Length > 0;
        }

        /// <inheritdoc />
        public async Task<T> GetAsync<T>(string blobId,
                                         CancellationToken cancellationToken = default)
        {
            var blobData = await GetAsync<T>(blobId, null, cancellationToken);
            if (blobData == null)
            {
                return default;
            }

            return blobData.Data;
        }

        /// <inheritdoc />
        public async Task<BlobData<T>> GetAsync<T>(string blobId, 
                                                   IList<string> existingPropertiesOrMetaData = default, 
                                                   CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(blobId))
            {
                throw new ArgumentException(nameof(blobId));
            }

            var result = new BlobData<T>();

            string data;

            using (var stream = new MemoryStream())
            {
                var blobData = await GetBlobDataAsync(blobId, stream, cancellationToken);

                if (stream.Length <= 0)
                {
                    //  Blob didn't exist or the stream failed to get connected.
                    return default;
                }

                if (existingPropertiesOrMetaData != null)
                {
                    // Try extracting some meta data or blob properties.
                    result.MetaData = ExtractMetaDataAndProperties(blobData.BlobProperties,
                                                                   blobData.MetaData,
                                                                   existingPropertiesOrMetaData);
                }

                stream.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(stream))
                {
                    data = await reader.ReadToEndAsync();
                }
            }


            if (string.IsNullOrWhiteSpace(data))
            {
                return null;
            }

            if (typeof(T).IsASimpleType())
            {
                // Assumption: Item was stored 'raw' and not serialized as Json.
                //             No need to do anything special, just use the current data.
                result.Data = (T)Convert.ChangeType(data, typeof(T));
            }
            else
            {
                // Assumption: Item was probably serialized (because it was not a simple type), so we now deserialize it.
                try
                {
                    result.Data = JsonConvert.DeserializeObject<T>(data);
                }
                catch(Exception exception)
                {
                    var errorData = data?.Length >= 15
                        ? data.Substring(0, 12) + "..." // 15 - 3 == 12. 3 == the 3x dots. e.g. "some longer ..."
                        : data
                        ?? "- no data -";
                    var errorLength = data?.Length ?? 0;
                    throw new JsonReaderException($"Failed to deserialize the json data [{errorData}], length: [{errorLength}].", exception);
                }
            }

            return result;
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string blobId,
                                      CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(blobId))
            {
                throw new ArgumentException(nameof(blobId));
            }

            var blob = (await Container).GetBlockBlobReference(blobId);
            if (blob != null &&
                await blob.ExistsAsync(cancellationToken))
            {
                await blob.DeleteAsync(cancellationToken);
            }
        }

        /// <inheritdoc />
        public async Task<string> AddAsync(object item,
                                           string blobId = null,
                                           CancellationToken cancellationToken = default)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            return await AddAsync(item, blobId, Encoding.UTF8, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<string> AddAsync(object item,
                                           string blobId,
                                           Encoding encoding,
                                           CancellationToken cancellationToken = default)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            string data;
            string contentType;

            if (item.GetType().IsASimpleType())
            {
                // No need to convert this item to json.
                data = item.ToString();
                contentType = "text/plain";
            }
            else
            {
                data = JsonConvert.SerializeObject(item);
                contentType = "application/json";
            }

            var bytes = encoding.GetBytes(data);

            return await AddAsync(bytes, blobId, contentType, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<string> AddAsync(byte[] content,
                                           string blobId = null,
                                           string contentType = null,
                                           CancellationToken cancellationToken = default)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            string generatedBlobName;
            using (var stream = new MemoryStream(content)
            {
                Position = 0
            })
            {
                generatedBlobName = await AddAsync(stream,
                                                   blobId,
                                                   contentType,
                                                   cancellationToken);
            }

            return generatedBlobName;
        }

        /// <inheritdoc />
        public async Task<string> AddAsync(Stream content,
                                           string blobId = null,
                                           string contentType = null,
                                           CancellationToken cancellationToken = default)
        {
            if (content == null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            if (string.IsNullOrWhiteSpace(blobId))
            {
                blobId = Guid.NewGuid().ToString();
            }

            var blob = (await Container).GetBlockBlobReference(blobId);
            if (blob == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                blob.Properties.ContentType = contentType;
            }

            await blob.UploadFromStreamAsync(content, cancellationToken);

            return blob.Name;
        }

        /// <inheritdoc />
        public async Task<string> AddAsync(Uri sourceUri,
                                           string blobId = null,
                                           string contentType = null,
                                           CancellationToken cancellationToken = default)
        {
            if (sourceUri == null)
            {
                throw new ArgumentNullException(nameof(sourceUri));
            }

            if (string.IsNullOrWhiteSpace(blobId))
            {
                blobId = Guid.NewGuid()
                             .ToString();
            }

            var blob = (await Container).GetBlockBlobReference(blobId);
            if (blob == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                blob.Properties.ContentType = contentType;
            }

            await blob.StartCopyAsync(sourceUri, cancellationToken);

            bool copyInProgress = true;
            while (copyInProgress)
            {
                // Urgh ... yes ... we have to wait a wee bit before we can check the
                // if the copy has completed. :(
                await Task.Delay(500, cancellationToken);

                await blob.FetchAttributesAsync(cancellationToken);

                copyInProgress = blob.CopyState.Status == CopyStatus.Pending;
            }

            return blob.Name;
        }

        /// <inheritdoc />
        public async Task<IList<string>> AddBatchAsync<T>(ICollection<T> items,
                                                          int batchSize = 25,
                                                          CancellationToken cancellationToken = default)
        {
            return await AddBatchAsync(items, Encoding.UTF8, batchSize, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IList<string>> AddBatchAsync<T>(ICollection<T> items,
                                                          Encoding encoding,
                                                          int batchSize = 25,
                                                          CancellationToken cancellationToken = default)
        {
            if (batchSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(batchSize));
            }

            var itemCount = items.Count;
            var finalBatchSize = itemCount > batchSize
                                     ? batchSize
                                     : itemCount;

            var blobIds = new List<string>();

            foreach (var batch in items.Batch(finalBatchSize))
            {
                var tasks = batch.Select(item => AddAsync(item, null, encoding, cancellationToken));

                // Execute the batch! Go Go Go!
                var results = await Task.WhenAll(tasks);

                blobIds.AddRange(results);
            }

            return blobIds;
        }

        private async Task<CloudBlobContainer> CreateCloudBlobContainer(BlobContainerPermissions permissions)
        {
            // TODO: Add POLLY retrying.

            if (!CloudStorageAccount.TryParse(_connectionString, out var storageAccount))
            {
                _logger.LogError($"Failed to create an Azure Storage Account for the provided credentials. Check the connection string in the your configuration (appsettings or environment variables, etc).");
                throw new Exception("Failed to create an Azure Storage Account.");
            }

            var cloudBlobClient = storageAccount.CreateCloudBlobClient();
            var cloudBlobContainer = cloudBlobClient.GetContainerReference(Name);

            var created = await cloudBlobContainer.CreateIfNotExistsAsync();
            if (created)
            {
                _logger.LogInformation("  - No Azure Blob [{blobName}] found - so one was auto created.", Name);
            }
            else
            {
                _logger.LogInformation("  - Using existing Azure Blob [{blobName}].", Name);
            }

            if (permissions != null)
            {
                await cloudBlobContainer.SetPermissionsAsync(permissions);
            }

            return cloudBlobContainer;
        }

        private async Task<(BlobProperties BlobProperties,
                            IDictionary<string, string> MetaData)> GetBlobDataAsync(string blobName,
                                                                                    Stream stream,
                                                                                    CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(blobName))
            {
                throw new ArgumentException(nameof(blobName));
            }

            var blob = (await Container).GetBlockBlobReference(blobName);
            if (blob == null ||
                !await blob.ExistsAsync(cancellationToken))
            {
                return (null, null);
            }

            await blob.DownloadToStreamAsync(stream, cancellationToken);

            return (blob.Properties, blob.Metadata);
        }

        private IDictionary<string, object> ExtractMetaDataAndProperties(BlobProperties blobProperties, 
                                                                         IDictionary<string, string> metaData,
                                                                         IList<string> existingPropertiesOrMetaData)
        {
            if (blobProperties is null)
            {
                throw new ArgumentNullException(nameof(blobProperties));
            }

            if (existingPropertiesOrMetaData is null)
            {
                throw new ArgumentNullException(nameof(existingPropertiesOrMetaData));
            }

            var result = new Dictionary<string, object>();

            // Copy over any properties we expect.
            foreach (var key in existingPropertiesOrMetaData)
            {
                var property = typeof(BlobProperties).GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (property != null)
                {
                    // The key is a blob property.
                    result.Add(key, property.GetValue(blobProperties));
                }
                else if (metaData?.Any() == true &&
                    metaData.ContainsKey(key))
                {
                    // We have some MetaData AND the key is some MetaData.
                    result.Add(key, metaData[key]);
                }
                else
                {
                    // Expected key doesn't exist in either. Not good :(
                    throw new Exception($"BlobProperties and MetaData doesn't contain the expected key: [{key}]. At least one of them should contain that key.");
                }
            }
            
            return result;
        }
    }
}
