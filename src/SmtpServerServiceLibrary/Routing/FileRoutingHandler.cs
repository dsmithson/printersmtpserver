using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace SmtpServerServiceLibrary.Routing
{
    public class FileRoutingHandler : IRoutingHandler
    {
        public Task<bool> ProcessMessage(MailMessage message)
        {
            throw new NotImplementedException();
        }
    }
}
