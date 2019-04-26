using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Homely.Storage.Blobs.Tests
{
    public class DeleteAsyncTests : CommonTestSetup
    {
        [Fact]
        public async Task GivenAnExistingBlob_DeleteAsync_DeletesTheBlob()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();
            var blobId = await SetupDeleteTestsAsync(azureBlob);

            // Act.
            await azureBlob.DeleteAsync(blobId);

            // Assert.
            var blob = await azureBlob.GetAsync(blobId);
            blob.ShouldBeNull();
        }

        [Fact]
        public async Task GivenAnMissingBlob_DeleteAsync_DoesNotThrowAnError()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();
            var blobId = await SetupDeleteTestsAsync(azureBlob);

            // Act.
            await azureBlob.DeleteAsync(blobId);

            // Assert.
            var blob = await azureBlob.GetAsync(blobId);
            blob.ShouldBeNull();
        }

        private async Task<string> SetupDeleteTestsAsync(IBlob blob)
        {
            if (blob == null)
            {
                throw new ArgumentNullException(nameof(blob));
            }

            var blobId = Guid.NewGuid().ToString();

            await blob.AddAsync(TestUser, blobId);

            return blobId;
        }
    }
}
