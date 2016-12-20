using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Web;
using Orchard.ContentManagement;
using Orchard.Environment.Extensions;
using Orchard.FileSystems.Media;
using Orchard.Forms.Services;
using Orchard.Logging;
using Orchard.MediaProcessing.Descriptors.Filter;
using Orchard.MediaProcessing.Media;
using Orchard.MediaProcessing.Models;
using Orchard.MediaProcessing.Services;
using Orchard.Tokens;
using Orchard.Utility.Extensions;

namespace Orchard.Aliyun.MediaProcessing.Services {

    [OrchardSuppressDependency("Orchard.MediaProcessing.Services.ImageProfileManager")]
    public class ImageProfileManager : IImageProfileManager {
        private readonly IStorageProvider _storageProvider;
        private readonly IImageProcessingFileNameProvider _fileNameProvider;
        private readonly IImageProfileService _profileService;
        private readonly IImageProcessingManager _processingManager;
        private readonly IOrchardServices _services;
        private readonly ITokenizer _tokenizer;

        public ImageProfileManager(
            IStorageProvider storageProvider,
            IImageProcessingFileNameProvider fileNameProvider,
            IImageProfileService profileService,
            IImageProcessingManager processingManager,
            IOrchardServices services,
            ITokenizer tokenizer) {
            _storageProvider = storageProvider;
            _fileNameProvider = fileNameProvider;
            _profileService = profileService;
            _processingManager = processingManager;
            _services = services;
            _tokenizer = tokenizer;

            Logger = NullLogger.Instance;
        }

        public ILogger Logger { get; set; }

          public string GetImageProfileUrl(string path, string profileName) {
            return GetImageProfileUrl(path, profileName, null, new FilterRecord[] { });
        }

        public string GetImageProfileUrl(string path, string profileName, ContentItem contentItem) {
            return GetImageProfileUrl(path, profileName, null, contentItem);
        }

        public string GetImageProfileUrl(string path, string profileName, FilterRecord customFilter) {
            return GetImageProfileUrl(path, profileName, customFilter, null);
        }

        public string GetImageProfileUrl(string path, string profileName, FilterRecord customFilter, ContentItem contentItem) {
            var customFilters = customFilter != null ? new FilterRecord[] { customFilter } : null;
            return GetImageProfileUrl(path, profileName, contentItem, customFilters);
        }

        public string GetImageProfileUrl(string path, string profileName, ContentItem contentItem, params FilterRecord[] customFilters) {

            // path is the publicUrl of the media, so it might contain url-encoded chars
            var decodedPath = System.Web.HttpUtility.UrlDecode(path);

            // generate a timestamped url to force client caches to update if the file has changed
            var publicUrl = _storageProvider.GetPublicUrl(decodedPath) + "!200_200";
            var timestamp = _storageProvider.GetFile(decodedPath).GetLastUpdated().Ticks;
            return publicUrl + "?v=" + timestamp.ToString(CultureInfo.InvariantCulture);
        }

        // TODO: Update this method once the storage provider has been updated
        private Stream GetImage(string path) {
            if (path == null) {
                throw new ArgumentNullException("path");
            }

            var storagePath = _storageProvider.GetStoragePath(path);
            if (storagePath != null) {
                try {
                    var file = _storageProvider.GetFile(storagePath);
                    return file.OpenRead();
                }
                catch(Exception e) {
                    Logger.Error(e, "path:" + path + " storagePath:" + storagePath);
                }
            }

            // http://blob.storage-provider.net/my-image.jpg
            if (Uri.IsWellFormedUriString(path, UriKind.Absolute)) {
                return new WebClient().OpenRead(new Uri(path));
            }

            // ~/Media/Default/images/my-image.jpg
            if (VirtualPathUtility.IsAppRelative(path)) {
                var request = _services.WorkContext.HttpContext.Request;
                return new WebClient().OpenRead(new Uri(request.Url, VirtualPathUtility.ToAbsolute(path)));
            }

            return null;
        }

        private bool TryGetImageLastUpdated(string path, out DateTime lastUpdated) {
            var storagePath = _storageProvider.GetStoragePath(path);
            if (storagePath != null) {
                var file = _storageProvider.GetFile(storagePath);
                lastUpdated = file.GetLastUpdated();
                return true;
            }

            lastUpdated = DateTime.MinValue;
            return false;
        }

        private string FormatProfilePath(string profileName, string path) {
            
            var filenameWithExtension = Path.GetFileName(path) ?? "";
            var fileLocation = path.Substring(0, path.Length - filenameWithExtension.Length);

            return _storageProvider.Combine(
                _storageProvider.Combine(profileName.GetHashCode().ToString("x").ToLowerInvariant(), fileLocation.GetHashCode().ToString("x").ToLowerInvariant()),
                    filenameWithExtension);
        }
    }
}
