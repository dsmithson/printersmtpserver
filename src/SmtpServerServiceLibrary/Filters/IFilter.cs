using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace SmtpServerServiceLibrary.Filters
{
    public interface IFilter
    {
        bool FilterMatchesMessage(MailMessage message);
    }
}
