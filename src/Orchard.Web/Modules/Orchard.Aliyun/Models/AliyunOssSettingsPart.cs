using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;
using Orchard.ContentManagement;

namespace Orchard.Aliyun.Models {
    public class AliyunOssSettingsPart : ContentPart {
        [Required]
        public string Endpoint
        {
            get { return this.Retrieve(x => x.Endpoint); }
            set { this.Store(x => x.Endpoint, value); }
        }

        [Required]
        public string BucketName
        {
            get { return this.Retrieve(x => x.BucketName); }
            set { this.Store(x => x.BucketName, value); }
        }

        [Required]
        public string PublicEntryUrl
        {
            get { return this.Retrieve(x => x.PublicEntryUrl); }
            set { this.Store(x => x.PublicEntryUrl, value); }
        }

        [Required]
        public string AccessKeyId
        {
            get { return this.Retrieve(x => x.AccessKeyId); }
            set { this.Store(x => x.AccessKeyId, value); }
        }

        [Required]
        public string AccessKeySecret
        {
            get { return this.Retrieve(x => x.AccessKeySecret); }
            set { this.Store(x => x.AccessKeySecret, value); }
        }
    }
}