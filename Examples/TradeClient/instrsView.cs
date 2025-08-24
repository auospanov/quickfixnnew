using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeClient
{
    public class instrsView
    {
        [NotMapped]
        public string requestId { get; set; }
        public string symbol { get; set; }
        public string codeMubasher { get; set; }       
    }

}
