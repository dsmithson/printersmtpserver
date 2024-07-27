using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmtpServerServiceLibrary.Attachments
{
    public abstract class Attachment
    {
        public string AttachmentName { get; set; }

        public abstract Stream GetStream();
    }
}
