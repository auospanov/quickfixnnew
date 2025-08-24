using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TradeClient
{
    [XmlRoot(ElementName = "Envelope", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
    public class SoapEnvelope2
    {
        [XmlElement(ElementName = "Body", Namespace = "http://schemas.xmlsoap.org/soap/envelope/")]
        public SoapBody2 Body { get; set; }
    }

    public class SoapBody2
    {
        [XmlElement(ElementName = "retrieveGetHistoryResponse", Namespace = "http://services.bloomberg.com/datalicense/dlws/ps/20071001")]
        public RetrieveGetHistoryResponse RetrieveGetHistoryResponse { get; set; }
    }

    public class RetrieveGetHistoryResponse
    {
        [XmlElement(ElementName = "statusCode", Namespace = "http://services.bloomberg.com/datalicense/dlws/ps/20071001")]
        public StatusCode StatusCode { get; set; }

        [XmlElement(ElementName = "requestId", Namespace = "http://services.bloomberg.com/datalicense/dlws/ps/20071001")]
        public string RequestId { get; set; }

        [XmlElement(ElementName = "responseId", Namespace = "http://services.bloomberg.com/datalicense/dlws/ps/20071001")]
        public string ResponseId { get; set; }

        [XmlArray(ElementName = "instrumentDatas", Namespace = "http://services.bloomberg.com/datalicense/dlws/ps/20071001")]
        [XmlArrayItem(ElementName = "instrumentData", Namespace = "http://services.bloomberg.com/datalicense/dlws/ps/20071001")]
        public List<InstrumentData> InstrumentDatas { get; set; }
    }


    public class InstrumentData
    {
        [XmlElement(ElementName = "instrument", Namespace = "http://services.bloomberg.com/datalicense/dlws/ps/20071001")]
        public Instrument Instrument { get; set; }

        [XmlElement(ElementName = "date", Namespace = "http://services.bloomberg.com/datalicense/dlws/ps/20071001")]
        public string Date { get; set; }

        [XmlElement(ElementName = "data", Namespace = "http://services.bloomberg.com/datalicense/dlws/ps/20071001")]
        public List<DataValue> Data { get; set; }
    }

    public class Instrument
    {
        [XmlElement(ElementName = "id", Namespace = "http://services.bloomberg.com/datalicense/dlws/ps/20071001")]
        public string Id { get; set; }
    }

    public class DataValue
    {
        [XmlAttribute(AttributeName = "value")]
        public string Value { get; set; }
    }
}
