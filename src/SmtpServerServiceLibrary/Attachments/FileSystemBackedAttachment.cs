using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmtpServerServiceLibrary.Attachments
{
    public class FileSystemBackedAttachment : Attachment
    {
        /// <summary>
        /// Temporary path to where the attachment is being stored
        /// </summary>
        public string TempFilePath { get; set; }

        public FileSystemBackedAttachment()
        {

        }

        public FileSystemBackedAttachment(string tempFilePath)
        {
            this.TempFilePath = tempFilePath;
        }

        public override Stream GetStream()
        {
            if (string.IsNullOrEmpty(TempFilePath) || !File.Exists(TempFilePath))
                return null;

            return new FileStream(TempFilePath, FileMode.Open);
        }
    }
}
