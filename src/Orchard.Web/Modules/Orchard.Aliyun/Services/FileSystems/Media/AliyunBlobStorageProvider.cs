using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Orchard.Environment.Configuration;
using Orchard.ContentManagement;
using Orchard.FileSystems.Media;
using Orchard.Logging;
using Aliyun.OSS;
using Aliyun.OSS.Common;
using System.Drawing;
using Orchard.Aliyun.Models;

namespace Orchard.Aliyun.Services.FileSystems.Media {
    
    public class AliyunBlobStorageProvider : IStorageProvider {
        
        private readonly ShellSettings _shellSettings;
        private readonly IMimeTypeProvider _mimeTypeProvider;
        private readonly IOrchardServices _orchardServices;
       

        private AliyunOssSettingsPart Settings
        {
            get { return _orchardServices.WorkContext.CurrentSite.As<AliyunOssSettingsPart>(); }
        }

        private string BucketName {
            get { return Settings.BucketName; }
        }
        private string Endpoint
        {
            get { return Settings.Endpoint; }
        }

        private string PublicEntryUrl
        {
            get { return Settings.PublicEntryUrl; }
        }

        private string AccessKeyId
        {
            get { return Settings.AccessKeyId; }
        }

        private string AccessKeySecret
        {
            get { return Settings.AccessKeySecret; }
        }

        private OssClient OssClient
        {
            get {
                if (this.Endpoint == null)
                    throw new InvalidDataException("Aliyun OSS Endpoint Setting should not be null");
                if (this.AccessKeyId == null)
                    throw new InvalidDataException("Aliyun OSS AccessKeyId should not be null");
                if(this.AccessKeySecret == null)
                    throw new InvalidDataException("Aliyun OSS AccessKeySecret should not be null");

                //Construct a new OSSClient instance
                var conf = new ClientConfiguration();
                conf.MaxErrorRetry = 3;     //设置请求发生错误时最大的重试次数
                conf.ConnectionTimeout = 300000;  //设置连接超时时间
                return new OssClient(this.Endpoint, this.AccessKeyId, this.AccessKeySecret, conf);

                //TODO:
                //目前我们每次Get都会创建一个新的OssClient对象，为了更好的性能我们应该考虑缓存OssClient
                //每次从缓存中取得OssClient对象，为了确保参数正确，每次得到缓存的OssClient后再重新设置相关的属性

            }
        }

        private ILogger Logger { get; set; }

        public AliyunBlobStorageProvider(
            ShellSettings shellSettings, 
            IMimeTypeProvider mimeTypeProvider,
            IOrchardServices orchardServices) { 
            _shellSettings = shellSettings;
            _mimeTypeProvider = mimeTypeProvider;
            _orchardServices = orchardServices;

            Logger = NullLogger.Instance;
        }

        public string Combine(string path1, string path2) {
            return Path.Combine(path1, path2);
        }

        public void CopyFile(string originalPath, string duplicatePath) {
            throw new NotImplementedException();
        }

        public IStorageFile CreateFile(string path) {
            return new AliyunOssStorageFile(OssClient, path);
        }

        public void CreateFolder(string path) {
            try {
                // 重要: 这时候作为目录的key必须以斜线（／）结尾
                string key = path.EndsWith("/") ? path : path + "/";

                if (key.StartsWith("/")) {
                    key = key.Substring(1);
                }
                
                // 此时的目录是一个内容为空的文件
                using (var stream = new MemoryStream()) {
                    OssClient.PutObject(this.BucketName, key, stream);
                    Console.WriteLine("Create dir {0} succeeded", key);
                }
            }
            catch (Exception ex) {
                Console.WriteLine("Create dir failed, {0}", ex.Message);
            }
        }

        public void DeleteFile(string path) {
            throw new NotImplementedException();
        }

        public void DeleteFolder(string path) {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 通过传入的路径Path，调用Aliyun OSS的文件List方法判断是否存在该文件
        /// 
        /// 另外可以考虑是否采用网络请求资源路径（HEAD请求），通过是否返回404来判断文件是否存在
        /// </summary>
        /// <param name="path">文件在Orchard Media Libaray中的相对路径</param>
        /// <returns></returns>
        public bool FileExists(string path) {
            try {
                var listObjectsRequest = new ListObjectsRequest(this.BucketName) {
                    Prefix = path.Substring(1)
                };
                var result = OssClient.ListObjects(listObjectsRequest);

                Console.WriteLine("List object succeeded");
                
                if (result.ObjectSummaries.Any(summary => summary.Key == path.Substring(1))) {
                    return true;
                }
            }
            catch (Exception ex) {
                Console.WriteLine("List object failed, {0}", ex.Message);
            }
            return false;
        }

        public bool FolderExists(string path) {
            try {
                var listObjectsRequest = new ListObjectsRequest(this.BucketName) {
                    Prefix = path.Substring(1)
                };
                var result = OssClient.ListObjects(listObjectsRequest);

                Console.WriteLine("List object succeeded");

                if (result.CommonPrefixes.Any(prefix => prefix == path.Substring(1))) {
                    return true;
                }
            }
            catch (Exception ex) {
                Console.WriteLine("List object failed, {0}", ex.Message);
            }
            return false;
        }

        public IStorageFile GetFile(string path) {
            try {
                //var listObjectsRequest = new ListObjectsRequest(_bucketName) {
                //    Prefix = path.Substring(1)
                //};
                //var result = _ossClient.ListObjects(listObjectsRequest);

                //Console.WriteLine("List object succeeded");
                //var ossObjectSummary = result.ObjectSummaries.First();
                //if (ossObjectSummary != null) {
                //    return new AliyunOssStorageFile(ossObjectSummary);
                //}
                var realPath = path;

                Uri uriResult;
                bool isAbsoluteUri = Uri.TryCreate(path, UriKind.Absolute, out uriResult)
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (isAbsoluteUri) {
                    realPath = System.Web.HttpUtility.UrlDecode(uriResult.AbsolutePath);
                }
                var metadata = OssClient.GetObjectMetadata(this.BucketName, realPath.Substring(1));
                

                Console.WriteLine("Get object meta succeeded");
                Console.WriteLine("Content-Type:{0}", metadata.ContentType);
                Console.WriteLine("Content Length: {0}", metadata.ContentLength);
                

                string width = "0";
                string height = "0";
                string mimeType = "";
                metadata.UserMetadata.TryGetValue("width", out width);
                metadata.UserMetadata.TryGetValue("height", out height);
                metadata.UserMetadata.TryGetValue("mimeType", out mimeType);


                return new AliyunOssStorageFile(
                    path, 
                    int.Parse(width == null ? "0" : width), 
                    int.Parse(height == null ? "0" : height), 
                    mimeType, 
                    metadata.ContentLength,
                    metadata.LastModified
                    );
            }
            catch (Exception ex) {
                Console.WriteLine("List object failed, {0}", ex.Message);
            }
            return null; 
        }

        public string GetPublicUrl(string path) {
            var baseUri = new Uri(PublicEntryUrl);
            return new Uri(baseUri, path).AbsoluteUri;
        }

        public string GetStoragePath(string url) {
            try {
                return new Uri(url).AbsolutePath;
            }catch(Exception ex) {
                Logger.Error("Get storage path of {0} failed, {1}", url, ex.Message);
                return "/404.jpg";
            }   
        }

        public IEnumerable<IStorageFile> ListFiles(string path) {
            List<IStorageFile> files = new List<IStorageFile>();

            try {
                var listObjectsRequest = new ListObjectsRequest(this.BucketName) {
                    Delimiter = "/",
                    Prefix = path == null ? string.Empty : path.Substring(1),
                    MaxKeys = 1000,
                    Marker = string.Empty
                };
                var result = OssClient.ListObjects(listObjectsRequest);

                Console.WriteLine("List object succeeded");
                //foreach (var summary in result.ObjectSummaries) {
                //    folders.Add(new AliyunOssStorageFolder())
                //}
                foreach (var summary in result.ObjectSummaries) {
                    files.Add(new AliyunOssStorageFile(summary));
                }
            }
            catch (Exception ex) {
                Console.WriteLine("List object failed, {0}", ex.Message);
            }

            return files.AsEnumerable<IStorageFile>();
        }

        public IEnumerable<IStorageFolder> ListFolders(string path) {
            
            List<IStorageFolder> folders = new List<IStorageFolder>();

            try {
                var listObjectsRequest = new ListObjectsRequest(this.BucketName) {
                    Delimiter = "/",
                    Prefix = path == null ? string.Empty : path.Substring(1),
                    MaxKeys = 1000,
                    Marker = string.Empty
                };
                var result = OssClient.ListObjects(listObjectsRequest);

                Console.WriteLine("List object succeeded");
                //foreach (var summary in result.ObjectSummaries) {
                //    folders.Add(new AliyunOssStorageFolder())
                //}
                foreach(var prefix in result.CommonPrefixes) {
                    folders.Add(new AliyunOssStorageFolder(prefix, DateTime.Now, "/" + prefix, 0, null));
                }
            }
            catch (Exception ex) {
                Console.WriteLine("List object failed, {0}", ex.Message);
            }
            
            return folders.AsEnumerable<IStorageFolder>();
        }

        public void RenameFile(string oldPath, string newPath) {
            throw new NotImplementedException();
        }

        public void RenameFolder(string oldPath, string newPath) {
            throw new NotImplementedException();
        }

        public void SaveStream(string path, Stream inputStream) {
            string key;
            if (path.StartsWith("/")) {
                key = path.Substring(1);
            }
            else {
                throw new Exception("The path must be leading by '/' ");
            }

            var mimeType = _mimeTypeProvider.GetMimeType(path);
            var metadata = new ObjectMetadata();
            metadata.UserMetadata.Add("mimeType", mimeType);

            //-------------------------------------------
            //  Check the image extension - If it is image to through image process flow
            //-------------------------------------------
            if (Path.GetExtension(path).ToLower() == ".jpg"
                || Path.GetExtension(path).ToLower() == ".png"
                || Path.GetExtension(path).ToLower() == ".gif"
                || Path.GetExtension(path).ToLower() == ".jpeg"
                || Path.GetExtension(path).ToLower() == ".bmp") {

                try {
                    using (var image = Image.FromStream(inputStream)) {

                        // http://stackoverflow.com/a/23400751/4741442
                        // Uploading an image taken with an iPhone (for example), can cause incorrect orientation, 
                        // because the meta data (EXIF data) from the rotation is not read.
                        // So we have to fix it at uploading time
                        var rawFormat = image.RawFormat;

                        if (Array.IndexOf(image.PropertyIdList, 274) > -1) {
                            var orientation = (int)image.GetPropertyItem(274).Value[0];
                            switch (orientation) {
                                case 1:
                                    // No rotation required.
                                    break;
                                case 2:
                                    image.RotateFlip(RotateFlipType.RotateNoneFlipX);
                                    break;
                                case 3:
                                    image.RotateFlip(RotateFlipType.Rotate180FlipNone);
                                    break;
                                case 4:
                                    image.RotateFlip(RotateFlipType.Rotate180FlipX);
                                    break;
                                case 5:
                                    image.RotateFlip(RotateFlipType.Rotate90FlipX);
                                    break;
                                case 6:
                                    image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                                    break;
                                case 7:
                                    image.RotateFlip(RotateFlipType.Rotate270FlipX);
                                    break;
                                case 8:
                                    image.RotateFlip(RotateFlipType.Rotate270FlipNone);
                                    break;
                            }
                            // This EXIF data is now invalid and should be removed.
                            image.RemovePropertyItem(274);
                        }

                        metadata.UserMetadata.Add("width", image.Width.ToString());
                        metadata.UserMetadata.Add("height", image.Height.ToString());

                        using (MemoryStream mStream = new MemoryStream()) {
                            image.Save(mStream, rawFormat);
                            mStream.Position = 0; // If excluded, SaveStream can't read the memory stream

                            OssClient.PutObject(this.BucketName, key, mStream, metadata);
                            Console.WriteLine("Create dir {0} succeeded", path);
                        }

                    }
                }
                //catch (ArgumentException) {
                //    // Still trying to get .ico dimensions when it's blocked in System.Drawing, 
                //    // see: https://github.com/OrchardCMS/Orchard/issues/4473

                //    //if (mimeType != "image/x-icon" && mimeType != "image/vnd.microsoft.icon") {
                //    //    throw;
                //    //}
                //    //inputStream.Seek(0, SeekOrigin.Begin);
                //    //TryFillDimensionsForIco(inputStream, metadata);
                //}
                catch (Exception ex) {
                    Logger.Error("Save file failed, {0}", ex.Message);
                }
            }
            else {
                try {
                    OssClient.PutObject(this.BucketName, key, inputStream, metadata);
                    Console.WriteLine("Create dir {0} succeeded", path);
                }
                catch (Exception ex) {
                    Logger.Error("Save file failed, {0}", ex.Message);
                }
            }
            
        }


        private void TryFillDimensionsForIco(Stream stream, ObjectMetadata metadata) {
            stream.Position = 0;
            using (var binaryReader = new BinaryReader(stream)) {
                // Reading out the necessary bytes that indicate the image dimensions. For the file format see:
                // http://en.wikipedia.org/wiki/ICO_%28file_format%29
                // Reading out leading bytes containing unneded information.
                binaryReader.ReadBytes(6);
                // Reading out dimensions. If there are multiple icons bundled in the same file then this is the first image.
                var width = binaryReader.ReadByte();
                var height = binaryReader.ReadByte();

                metadata.UserMetadata.Add("width", width.ToString());
                metadata.UserMetadata.Add("height", height.ToString());
            }
        }

        public bool TryCreateFolder(string path) {
            throw new NotImplementedException();
        }

        public bool TrySaveStream(string path, Stream inputStream) {
            throw new NotImplementedException();
        }


        public class AliyunOssStorageFolder : IStorageFolder {
            private string _name;
            private DateTime _lastUpdated;
            private string _path;
            private long _size;
            private IStorageFolder _parent;

            public AliyunOssStorageFolder() { }

            public AliyunOssStorageFolder(
                string name,
                DateTime lastUpdated,
                string path,
                long size,
                IStorageFolder parent) {

                this._name = name;
                this._lastUpdated = lastUpdated;
                this._path = path;
                this._size = size;
                this._parent = parent;

            }

            public DateTime GetLastUpdated() {
                return this._lastUpdated;
            }

            public string GetName() {
                return this._name;
            }

            public IStorageFolder GetParent() {
                return this._parent;
            }

            public string GetPath() {
                return this._path;
            }

            public long GetSize() {
                return this._size;
            }
        }

        public class AliyunOssStorageFile : IStorageFile {
            
            private OssObjectSummary _ossObjectSummary;
            private OssClient _ossClient;
            private AliyunOssStream _stream;
            private readonly string _mimeType;
            private readonly int _width;
            private readonly int _height;
            private readonly long _size;
            private readonly string _path;
            private readonly DateTime _lastModified;

            public AliyunOssStorageFile(OssClient ossClient, string path) {
                _ossClient = ossClient;
                _path = path;
            }

            public AliyunOssStorageFile(
                string path, 
                int width, 
                int height, 
                string mimeType, 
                long size,
                DateTime lastModified) {
                _path = path;
                _width = width;
                _height = height;
                _mimeType = mimeType;
                _size = size;
                _lastModified = lastModified;
            }

            public AliyunOssStorageFile(OssObjectSummary ossObjectSummary) {
                this._ossObjectSummary = ossObjectSummary;
                _path = "/" + this._ossObjectSummary.Key;
                _lastModified = ossObjectSummary.LastModified;
                _size = ossObjectSummary.Size;
               
            }


            public Stream CreateFile() {
                _stream = new AliyunOssStream(_ossObjectSummary);
                return OpenWrite();
            }

            public string GetFileType() {
                return Path.GetExtension(Path.GetFileName(_path));
            }

            public DateTime GetLastUpdated() {
                return _lastModified;
            }

            public string GetName() {
                return Path.GetFileName(_path);
            }

            public string GetPath() {
                return _path;
            }

            public long GetSize() {
                return _size;
            }

            public int GetWidth() {
                return _width;
            }

            public int GetHeight() {
                return _height;
            }

            public string GetMimeType () {
                return _mimeType;
            }

            public DateTime GetLastModified() {
                return _lastModified;
            }

            public Stream OpenRead() {
                //If _stream does not exist, we initialize it first
                if(_stream == null) {
                    _stream = new AliyunOssStream(_ossObjectSummary);
                }
                return _stream;
            }

            public Stream OpenWrite() {
                return _stream;
            }
        }

        private class AliyunOssStream : Stream {

            private MemoryStream _storage;
            private OssObjectSummary _summary;
            

            public AliyunOssStream(OssObjectSummary summary) {
                _storage = new MemoryStream();
                _summary = summary;
            }

            public override bool CanRead
            {
                get
                {
                    return _storage.CanRead;
                }
            }

            public override bool CanSeek
            {
                get
                {
                    return _storage.CanSeek;
                }
            }

            public override bool CanWrite
            {
                get
                {
                    return _storage.CanWrite;
                }
            }

            public override long Length
            {
                get
                {
                    return _storage.Length;
                }
            }

            public override long Position
            {
                get
                {
                    return _storage.Position;
                }

                set
                {
                    _storage.Position = value;
                }
            }

            public override void Flush() {
                //Flush the content of Stream to final media
                _storage.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count) {
                return _storage.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin) {
                return _storage.Seek(offset, origin);
            }

            public override void SetLength(long value) {
                _storage.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count) {
                _storage.Write(buffer, offset, count);
            }
        }
    }
    
}