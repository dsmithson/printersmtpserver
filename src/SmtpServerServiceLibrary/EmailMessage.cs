using SmtpServerServiceLibrary.Attachments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmtpServerServiceLibrary
{
    class EmailMessage
    {
        public List<EmailAddress> To { get; set; }
        public EmailAddress From { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public List<Attachment> Attachments { get; set; }
        
        public EmailMessage()
        {
            To = new List<EmailAddress>();
            Attachments = new List<Attachment>();
        }
    }
}
