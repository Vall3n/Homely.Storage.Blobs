using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Logging;
using MoreLinq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        /// <inheritdoc  />
        public string Name { get; }

        private Task<CloudBlobContainer> Container
        {
            get { return _container.Value; }
        }

        /// <inheritdoc  />
        public async Task<Stream> GetAsync(string blobName,
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
                return null;
            }

            var stream = new MemoryStream();

            await blob.DownloadToStreamAsync(stream, cancellationToken);

            return stream;
        }

        /// <inheritdoc />
        public async Task<T> GetAsync<T>(string blobName,
                                         CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(blobName))
            {
                throw new ArgumentException(nameof(blobName));
            }

            string data;

            using (var stream = await GetAsync(blobName, cancellationToken))
            {
                if (stream == null)
                {
                    return default;
                }

                stream.Seek(0, SeekOrigin.Begin);
                using (var reader = new StreamReader(stream))
                {
                    data = await reader.ReadToEndAsync();
                }
            }

            if (string.IsNullOrWhiteSpace(data))
            {
                return default;
            }

            if (typeof(T).IsASimpleType())
            {
                // Assumption: Item was stored 'raw' and not serialized as Json.
                //             No need to do anything special, just use the current data.
                return (T)Convert.ChangeType(data, typeof(T));
            }

            // Assumption: Item was probably serialized (because it was not a simple type), so we now deserialize it.
            return JsonConvert.DeserializeObject<T>(data);
        }

        /// <inheritdoc />
        public async Task DeleteAsync(string blobName,
                                      CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(blobName))
            {
                throw new ArgumentException(nameof(blobName));
            }

            var blob = (await Container).GetBlockBlobReference(blobName);
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

            if (!CloudStorageAccount.TryParse(_connectionString, out CloudStorageAccount storageAccount))
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
    }
}