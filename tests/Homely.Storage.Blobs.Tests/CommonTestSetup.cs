using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Homely.Storage.Blobs.Tests
{
    public abstract class CommonTestSetup
    {
        protected const string TestImageName = "2018-tesla-model-x-p100d.jpg";

        protected SomeFakeUser TestUser => new SomeFakeUser
        {
            Name = "Elon Musk",
            Age = 40
        };

        protected async Task<(AzureBlob Blob, string ImageBlobId, string TestUserBlobId)> SetupAzureBlobAsync(bool setupInitialBlobData = true)
        {
            var logger = new NullLogger<AzureBlob>();
            var container = Guid.NewGuid().ToString(); // This is so all the files (for this individual test) as isolated into this container.
            var blob = new AzureBlob("UseDevelopmentStorage=true", container, logger);

            var imageBlobId = Guid.NewGuid().ToString();
            var testUserBlobId = Guid.NewGuid().ToString();

            if (setupInitialBlobData)
            {
                var image = await File.ReadAllBytesAsync(TestImageName);

                var tasks = new List<Task>();

                tasks.Add(blob.AddAsync(image, imageBlobId));
                tasks.Add(blob.AddAsync(TestUser, testUserBlobId));

                await Task.WhenAll(tasks);
            }

            return (blob, imageBlobId, testUserBlobId);
        }

        protected async Task<AzureBlob> GetAzureBlobAsync(bool setupInitialBlobData = true)
        {
            var (azureBlob, _, _) = await SetupAzureBlobAsync(setupInitialBlobData);

            return azureBlob;
        }
    }
}
