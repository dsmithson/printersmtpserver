using SmtpServerServiceLibrary.Filters;
using SmtpServerServiceLibrary.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmtpServerServiceLibrary
{
    public class RelayRoutingRule
    {
        /// <summary>
        /// Predicates to determine if the current routing rule applies to a supplied message
        /// </summary>
        public List<IFilter> MatchFilters { get; set; }

        /// <summary>
        /// Handler which actually does work to deliver a mail message
        /// </summary>
        public IRoutingHandler Handler { get; set; }

        public RelayRoutingRule()
        {

        }

        public RelayRoutingRule(IRoutingHandler handler, List<IFilter> matchFilters)
        {
            this.Handler = handler;
            this.MatchFilters = matchFilters;
        }

        public RelayRoutingRule(IRoutingHandler handler, params IFilter[] matchFilters)
        {
            this.Handler = handler;
            this.MatchFilters = new List<IFilter>();
            if(matchFilters != null && matchFilters.Length > 0)
            {
                foreach(var matchFilter in matchFilters)
                {
                    this.MatchFilters.Add(matchFilter);
                }
            }
        }
    }
}
