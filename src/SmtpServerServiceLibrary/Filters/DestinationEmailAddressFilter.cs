using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace SmtpServerServiceLibrary.Filters
{
    public class DestinationEmailAddressFilter : IFilter
    {
        public string DestinationEmailAddress { get; set; }

        public DestinationEmailAddressFilter()
        {

        }

        public DestinationEmailAddressFilter(string destinationEmailAddress)
        {
            this.DestinationEmailAddress = destinationEmailAddress;
        }


        public bool FilterMatchesMessage(MailMessage message)
        {
            if (string.IsNullOrEmpty(DestinationEmailAddress))
                return false;

            return message.To
                .Any(to => !string.IsNullOrEmpty(to.Address)
                && string.Compare(to.Address, DestinationEmailAddress, true) == 0);
        }
    }
}
