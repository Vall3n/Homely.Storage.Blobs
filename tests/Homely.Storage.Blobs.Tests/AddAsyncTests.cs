using Homely.Testing;
using Shouldly;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Homely.Storage.Blobs.Tests
{
    public class AddAsyncTests : CommonTestSetup
    {
        [Fact]
        public async Task GivenAnObjectThatDoesNotExist_AddAsync_AddsTheObject()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();

            // Act.
            var blobId = await azureBlob.AddAsync(TestUser);

            // Assert.
            blobId.ShouldNotBeNullOrEmpty();
            var blob = await azureBlob.GetAsync<SomeFakeUser>(blobId);
            blob.ShouldLookLike(TestUser);
        }

        [Fact]
        public async Task GivenSomeTextThatDoesNotExist_AddAsync_AddsTheObject()
        {
            // Arrange.
            const string text = "some text";
            var azureBlob = await GetAzureBlobAsync();

            // Act.
            var blobId = await azureBlob.AddAsync(text);

            // Assert.
            blobId.ShouldNotBeNullOrEmpty();
            var blob = await azureBlob.GetAsync<string>(blobId);
            blob.ShouldLookLike(text);
        }

        [Fact]
        public async Task GivenSomeObjectThatDoesExist_AddAsync_OverwritesTheObject()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();

            var user = new SomeFakeUser
            {
                Name = Guid.NewGuid().ToString(),
                Age = DateTime.Now.Second
            };
            var originalBlobId = await azureBlob.AddAsync(user);
            
            // Act.
            var blobId = await azureBlob.AddAsync(TestUser, originalBlobId); // BlobId to associate, exists.

            // Assert.
            blobId.ShouldNotBeNullOrEmpty();
            var blob = await azureBlob.GetAsync<SomeFakeUser>(blobId);
            blob.ShouldLookLike(TestUser);
        }

        [Fact]
        public async Task GivenAnObjectWhichHasUTF8CharactersButWeWantToAddItAsAscii_AddAsync_AddsTheObjectAsAscii()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();
            const string utf8Text = "chárêctërs";
            const string asciiText = "ch?r?ct?rs"; // NOTE: UTF8 characters are now 'lost'.

            // Act.
            var blobId = await azureBlob.AddAsync(utf8Text, null, Encoding.ASCII);

            // Assert.
            blobId.ShouldNotBeNullOrWhiteSpace();
            var blob = await azureBlob.GetAsync<string>(blobId);
            blob.ShouldLookLike(asciiText);
        }

        [Fact]
        public async Task GivenSomeBinaryData_AddAsync_AddsTheBinaryData()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();
            var bytes = await File.ReadAllBytesAsync(TestImageName);

            // Act.
            var blobId = await azureBlob.AddAsync(bytes);

            // Assert.
            var existingBlob = await azureBlob.GetAsync(blobId);
            existingBlob.ShouldNotBeNull();

            byte[] existingBytes;
            using (var ms = new MemoryStream())
            {
                existingBlob.Position = 0;
                existingBlob.CopyTo(ms);
                existingBytes = ms.ToArray();
            }

            existingBytes.Length.ShouldBe(bytes.Length);

            existingBlob.Dispose();
        }

        [Fact]
        public async Task GivenSomeStream_AddAsync_AddsTheStream()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();
            var stream = File.OpenRead(TestImageName);

            // Act.
            var blobId = await azureBlob.AddAsync(stream);

            // Assert.
            stream.Dispose();

            var existingBlob = await azureBlob.GetAsync(blobId);
            existingBlob.ShouldNotBeNull();
            existingBlob.Length.ShouldBeGreaterThan(0);
        }

        [Fact(Skip = "Azurite is unable to handle 'copy'ing Uri's. As such, the test will fail because Azurite throws an exception.")]
        public async Task GivenSomeUri_AddAsync_AddsTheUri()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();
            var uri = new Uri("http://www.google.com.au");

            // Act.
            var blobId = await azureBlob.AddAsync(uri);

            // Assert.
            var existingBlob = await azureBlob.GetAsync(blobId);
            existingBlob.ShouldNotBeNull();
            existingBlob.Length.ShouldBeGreaterThan(0);
        }
    }
}
