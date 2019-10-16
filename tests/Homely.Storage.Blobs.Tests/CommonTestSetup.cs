using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;

namespace Homely.Storage.Blobs.Tests
{
    public abstract class CommonTestSetup
    {
        protected string TestImageName = "2018-tesla-model-x-p100d.jpg";
        protected string TestClassInstanceName = "elon-musk";
        protected SomeFakeUser TestUser => new SomeFakeUser
        {
            Name = "Elon Musk",
            Age = 40
        };

        protected async Task<AzureBlob> GetAzureBlobAsync(bool setupInitialBlobData = true)
        {
            var logger = new NullLogger<AzureBlob>();
            var blob = new AzureBlob("UseDevelopmentStorage=true", "test-container", logger);

            if (setupInitialBlobData)
            {
                var cancellationToken = new CancellationToken();

                // Do we have the image already?
                if (await blob.GetAsync<string>(TestImageName, cancellationToken) == null)
                {
                    var image = await File.ReadAllBytesAsync("2018-tesla-model-x-p100d.jpg");
                    await blob.AddAsync(image, TestImageName);
                }

                if (await blob.GetAsync<SomeFakeUser>(TestClassInstanceName, cancellationToken) == null)
                {
                    await blob.AddAsync(TestUser, TestClassInstanceName);
                }
            }

            return blob;
        }
    }
}
