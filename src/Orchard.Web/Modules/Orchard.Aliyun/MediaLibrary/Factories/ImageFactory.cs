using System;
using System.IO;
using System.Linq;
using System.Drawing.Imaging;
using Orchard.ContentManagement;
using Orchard.ContentManagement.MetaData;
using Orchard.Environment.Extensions;
using Orchard.MediaLibrary.Factories;
using Orchard.MediaLibrary.Models;
using Orchard.FileSystems.Media;
using static Orchard.Aliyun.Services.FileSystems.Media.AliyunBlobStorageProvider;

namespace Orchard.Aliyun.MediaLibrary.Factories {

    [OrchardSuppressDependency("Orchard.MediaLibrary.Factories.ImageFactorySelector")]
    public class ImageFactorySelector : IMediaFactorySelector {
        private readonly IContentManager _contentManager;
        private readonly IContentDefinitionManager _contentDefinitionManager;
        private readonly IStorageProvider _storageProvider;

        public ImageFactorySelector(
            IContentManager contentManager, 
            IContentDefinitionManager contentDefinitionManager,
            IStorageProvider storageProvider) {
            _contentManager = contentManager;
            _contentDefinitionManager = contentDefinitionManager;
            _storageProvider = storageProvider;
        }


        public MediaFactorySelectorResult GetMediaFactory(Stream stream, string mimeType, string contentType) {
            if (!mimeType.StartsWith("image/")) {
                return null;
            }
            if (!ImageCodecInfo.GetImageDecoders().Select(d => d.MimeType).Contains(mimeType)) {
                return null;
            }

            if (!String.IsNullOrEmpty(contentType)) {
                var contentDefinition = _contentDefinitionManager.GetTypeDefinition(contentType);
                if (contentDefinition == null || contentDefinition.Parts.All(x => x.PartDefinition.Name != typeof(ImagePart).Name)) {
                    return null;
                }
            }

            return new MediaFactorySelectorResult {
                Priority = -5,
                MediaFactory = new ImageFactory(_contentManager, _storageProvider)
            };

        }
    }

   
    public class ImageFactory : IMediaFactory {
        private readonly IContentManager _contentManager;
        private readonly IStorageProvider _storageProvider;

        public ImageFactory(IContentManager contentManager, IStorageProvider storageProvider) {
            _contentManager = contentManager;
            _storageProvider = storageProvider;
        }

        public MediaPart CreateMedia(Stream stream, string path, string mimeType, string contentType) {
            if (String.IsNullOrEmpty(contentType)) {
                contentType = "Image";
            }

            var part = _contentManager.New<MediaPart>(contentType);

            part.LogicalType = "Image";
            part.MimeType = mimeType;
            part.Title = Path.GetFileNameWithoutExtension(path);

            var imagePart = part.As<ImagePart>();
            if (imagePart == null) {
                return null;
            }

            AliyunOssStorageFile fileInfo = (AliyunOssStorageFile)_storageProvider.GetFile(path);
            imagePart.Width = fileInfo.GetWidth();
            imagePart.Height = fileInfo.GetHeight();

            //try {
            //    using (var image = Image.FromStream(stream)) {
            //        imagePart.Width = image.Width;
            //        imagePart.Height = image.Height;
            //    }
            //}
            //catch (ArgumentException) {
            //    // Still trying to get .ico dimensions when it's blocked in System.Drawing, see: https://github.com/OrchardCMS/Orchard/issues/4473

            //    if (mimeType != "image/x-icon" && mimeType != "image/vnd.microsoft.icon") {
            //        throw;
            //    }

            //    TryFillDimensionsForIco(stream, imagePart);
            //}

            return part;
        }

        private void TryFillDimensionsForIco(Stream stream, ImagePart imagePart) {
            stream.Position = 0;
            using (var binaryReader = new BinaryReader(stream)) {
                // Reading out the necessary bytes that indicate the image dimensions. For the file format see:
                // http://en.wikipedia.org/wiki/ICO_%28file_format%29
                // Reading out leading bytes containing unneded information.
                binaryReader.ReadBytes(6);
                // Reading out dimensions. If there are multiple icons bundled in the same file then this is the first image.
                imagePart.Width = binaryReader.ReadByte();
                imagePart.Height = binaryReader.ReadByte();
            }
        }
    }
}