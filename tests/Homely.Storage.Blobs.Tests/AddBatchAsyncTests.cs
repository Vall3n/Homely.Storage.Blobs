using Shouldly;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Homely.Storage.Blobs.Tests
{
    public class AddBatchAsyncTests : CommonTestSetup
    {
        [Fact]
        public async Task GivenSomeObjectsToAdd_AddBatchAsync_AddsAllTheObjects()
        {
            // Arrange.
            var azureBlob = await GetAzureBlobAsync();
            var users = new List<SomeFakeUser>();
            for (var i = 0; i < 100; i++)
            {
                users.Add(new SomeFakeUser
                {
                    Name = Guid.NewGuid().ToString(),
                    Age = i
                });
            }

            // Act.
            var blobIds = await azureBlob.AddBatchAsync(users);

            // Assert.
            blobIds.ShouldNotBeEmpty();
            blobIds.Count.ShouldBe(100);
            foreach (var blobId in blobIds)
            {
                var existingUser = await azureBlob.GetAsync<SomeFakeUser>(blobId, default);
                users.ShouldContain(x => x.Name == existingUser.Name &&
                                         x.Age == existingUser.Age);
            }
        }

        [Fact]
        public async Task GivenSomeObjectsWithUft8CharactersButWeWantToAddItAsAscii_AddBatchAsync_AddsAllTheObjectsAsAscii()
        {
            // Arrange.
            const string utf8Text = "chárêctërs";
            const string asciiText = "ch?r?ct?rs"; // NOTE: UTF8 characters are now 'lost'.
            var azureBlob = await GetAzureBlobAsync();

            var users = new List<SomeFakeUser>();
            for (int i = 0; i < 100; i++)
            {
                users.Add(new SomeFakeUser
                {
                    Name = utf8Text,
                    Age = i
                });
            }

            // Act.
            var blobIds = await azureBlob.AddBatchAsync(users, Encoding.ASCII);

            // Assert.
            blobIds.ShouldNotBeEmpty();
            blobIds.Count.ShouldBe(100);
            foreach (var blobId in blobIds)
            {
                var existingUser = await azureBlob.GetAsync<SomeFakeUser>(blobId, default);
                existingUser.Name.ShouldBe(asciiText);
                users.ShouldContain(x => x.Age == existingUser.Age);
            }

        }
    }
}
