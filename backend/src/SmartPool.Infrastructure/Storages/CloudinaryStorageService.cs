using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmartPool.Application.Interfaces.Services;

namespace SmartPool.Infrastructure.Storages
{
    public class CloudinaryStorageService : IStorageService
    {
        public Task DeleteAsync(string fileUrl, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> DownloadAsync(string fileUrl, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
