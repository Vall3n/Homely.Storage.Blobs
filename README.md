# Homely - Storage Blobs library.

This library contains some helpers when working with Blob Storage.  
A common pattern when working with blobs is to serialize/deserialize complex objects to be stored in a blob. This library helps simplify this process - both ways. The content of the blob needs to be a string, so any value is converted either into a string representation (when a 'simple' type) -or- a string-JSON representation (when a 'complex' type). It can also handle byte arrays and streams, untouched.

### What's a Simple / Complex Type?
- Simple: a [.NET Primitive](https://docs.microsoft.com/en-us/dotnet/api/system.type.isprimitive?view=netframework-4.7.2#remarks) or `string` or `decimal`
- Complex: everything else. Usually a custom class.

### Azure Storage only?
The library is (currently) only targetting Azure Storage Blobs. Why not other blobs like AWS? Because:

- We (@Homely) don't work with AWS.
- There are [docker images](https://hub.docker.com/r/arafato/azurite/) for Azure Storage (for localhost development) - so we don't need other implementations @Homely.
- We will accept Pull Requeust for other platforms, though :heart:


# Samples

### Simple example.

```
// Create an instance of the Azure blob container.
var blob = new AzureBlob("some-connectionstring", "users", logger);

// Create a fake user.
var user = new User { Name = "Jane" };

// Add this user instance to the blob container. This will
// create a random Guid (as the blob identifier).
var blobId = await blob.AddAsync(user, cancellationToken);

// Retrieve the user.
var existingUser = await blob.GetAsync<User>(blobId, cancellationToken);

// Delete the user.
await blob.DeleteAsync(blobId);
```

### Methods

- `AddAsync` : this will accept any object, Byte array, Stream or Uri. If it is a simple object (like a `string` or `int`) it will just a insert that value _as is_ into the blob. Otherwise, it will serialize the object to JSON if it's _not_ a Byte/Stream/Uri.

e.g. 
```
var newBlobId = await blob.AddAsync(1);            // int '1' pushed as a string "1" => `application/text`
var newBlobId = await blob.AddAsync("hi there!");  // string 'Hi there' pushed. => `application/text`
var newBlobId = await blob.AddAsync(new Foo());    // serialized to JSON. => `application/json`
var newBlobId = await blob.AddAsync(myListOfFoos); // serialized to JSON. => `application/json`
var newBlobId = await blob.AddAsync(myStream);     // Stream converted to a binary/byte array content and then pushed up => `application/octec-stream`
var newBlobId = await blob.AddAsync(myBytes);      // Binary/byte array content then pushed up => `application/octec-stream`
```

You can also specify the Id of the blob you wish to store in the container:

e.g.
```
await blob.AddAsync(new Foo(), "user-1");
```

Also, you can add a collection of items in one simple call:

- `AddBatchAsync` : adds all the items in the collection, a batch at a time. Default batch size is 25 items. Returns all the blobId's for the created blobs.

e.g.
```
var foos = GetFoosFromSomewhere(); // Returns 100 foo's.
var blobIds = blob.AddBatchAsync(foos, 10); // Adds 100 foo's in batches of 10.
```

- `GetAsync` : this will retrieve a stream of the unknown object type. Then do as you please with it.
- `GetAsync<Foo>` : this will retrieve the item from the blob then attempt to deserialize the message into that complex object.

e.g.

```
// No 'Type' provided. Assumption: message content is not json and will therefore not be deserialized.
var myMessage = await blob.GeteAsync("some-blob-id"); // Stream to unknown blob type.

// Type is provided.
var myFoo = await blob.GetAsync<Foo>("some-foo-Id");  // A foo instance populated with it's respective blob information.
```

- `DeleteAsync` : removes the blob from the container.

e.g.

```
blob.DeleteAsync("some-blob-id");
```

---

## Contributing

Discussions and pull requests are encouraged :) Please ask all general questions in this repo or pick a specialized repo for specific, targetted issues. We also have a [contributing](https://github.com/Homely/Homely/blob/master/CONTRIBUTING.md) document which goes into detail about how to do this.

## Code of Conduct
Yep, we also have a [code of conduct](https://github.com/Homely/Homely/blob/master/CODE_OF_CONDUCT.md) which applies to all repositories in the (GitHub) Homely organisation.

## Feedback
Yep, refer to the [contributing page](https://github.com/Homely/Homely/blob/master/CONTRIBUTING.md) about how best to give feedback - either good or needs-improvement :)

---
