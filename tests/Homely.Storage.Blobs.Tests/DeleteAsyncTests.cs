using System.Threading.Tasks;
using Shouldly;
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

            // Act.
            await azureBlob.DeleteAsync(TestClassInstanceName);

            // Assert.
            var user = await azureBlob.GetAsync<SomeFakeUser>(TestClassInstanceName, default);
            user.ShouldBeNull();
        }

        [Fact]
        public async Task GivenAnMissingBlob_DeleteAsync_DoesNotThrowAnError()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();
            const string blobName = "aaa";

            // Act.
            await azureBlob.DeleteAsync(blobName);

            // Assert.
            var user = await azureBlob.GetAsync<SomeFakeUser>(blobName, default);
            user.ShouldBeNull();
        }
    }
}
