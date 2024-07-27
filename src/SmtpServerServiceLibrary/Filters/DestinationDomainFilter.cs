using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace SmtpServerServiceLibrary.Filters
{
    public class DestinationDomainFilter : IFilter
    {
        public string DestinationDomainName { get; set; }

        public DestinationDomainFilter()
        {

        }

        public DestinationDomainFilter(string destinationDomainName)
        {
            this.DestinationDomainName = destinationDomainName;
        }

        public bool FilterMatchesMessage(MailMessage message)
        {
            if (string.IsNullOrEmpty(DestinationDomainName))
                return false;

            return message.To.Any(to => !string.IsNullOrEmpty(to.Address) && to.Address.ToLower().EndsWith(DestinationDomainName.ToLower()));
        }
    }
}
