using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Orchard.Aliyun.Models;
using Orchard.ContentManagement;
using Orchard.ContentManagement.Handlers;
using Orchard.Localization;

namespace Orchard.Aliyun.Handlers {
    public class AliyunOssSettingsPartHandler : ContentHandler {
        public AliyunOssSettingsPartHandler() {
            T = NullLocalizer.Instance;
            Filters.Add(new ActivatingFilter<AliyunOssSettingsPart>("Site"));
            Filters.Add(new TemplateFilterForPart<AliyunOssSettingsPart>("AliyunOssSettings", "Parts/AliyunOssSettings", "aliyunOss"));
        }

        public Localizer T { get; set; }

        protected override void GetItemMetadata(GetContentItemMetadataContext context) {
            if (context.ContentItem.ContentType != "Site")
                return;
            base.GetItemMetadata(context);
            context.Metadata.EditorGroupInfo.Add(new GroupInfo(T("aliyunOss")));
        }
    }
}