using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
namespace TradeClient
{
    [XmlRoot(ElementName = "Envelope", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    public class SoapEnvelope
    {
        [XmlElement(ElementName = "Body")]
        public SoapBody Body { get; set; }
    }

    public class SoapBody
    {
        [XmlElement(ElementName = "submitGetHistoryResponse", Namespace = "http://services.bloomberg.com/datalicense/dlws/ps/20071001")]
        public SubmitGetHistoryResponse SubmitGetHistoryResponse { get; set; }
    }

    public class SubmitGetHistoryResponse
    {
        [XmlElement(ElementName = "statusCode")]
        public StatusCode StatusCode { get; set; }

        [XmlElement(ElementName = "requestId")]
        public string RequestId { get; set; }

        [XmlElement(ElementName = "responseId")]
        public string ResponseId { get; set; }
    }

    public class StatusCode
    {
        [XmlElement(ElementName = "code")]
        public int Code { get; set; }

        [XmlElement(ElementName = "description")]
        public string Description { get; set; }
    }

}