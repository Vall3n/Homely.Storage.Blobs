using Homely.Testing;
using Shouldly;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Homely.Storage.Blobs.Tests
{
    public class GetAsyncTests : CommonTestSetup
    {
        [Fact]
        public async Task GivenAnExistingStream_GetAsync_ReturnsTheBlobAsAStream()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();

            // Act.
            var stream = await azureBlob.GetAsync(TestImageName);

            // Assert.
            stream.ShouldNotBeNull();
        }

        [Fact]
        public async Task GivenAMissingStream_GetAsync_ReturnsNull()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();

            // Act.
            var stream = await azureBlob.GetAsync(Guid.NewGuid().ToString());

            // Assert.
            stream.ShouldBeNull();
        }

        [Fact]
        public async Task GivenAnExistingClassInstance_GetAsyncGeneric_ReturnsTheInstance()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();

            // Act.
            var user = await azureBlob.GetAsync<SomeFakeUser>(TestClassInstanceName);

            // Assert.
            user.ShouldNotBeNull();
            user.ShouldLookLike(TestUser);
        }

        [Fact]
        public async Task GivenAMissingClassInstance_GetAsyncGeneric_ReturnsNull()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();

            // Act.
            var user = await azureBlob.GetAsync<SomeFakeUser>(Guid.NewGuid().ToString());

            // Assert.
            user.ShouldBeNull();
        }
    }
}
