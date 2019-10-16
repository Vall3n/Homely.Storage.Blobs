using System.Collections.Generic;

namespace Homely.Storage.Blobs
{
    public class BlobData<T>
    {
        public BlobData()
        {
            MetaData = new Dictionary<string, object>();
        }

        public T Data { get; set; }
        public IDictionary<string, object> MetaData { get; set; }
    }
}
