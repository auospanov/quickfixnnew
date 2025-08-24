using System.IO;
using System.Text;
using System.Xml.Serialization;

namespace TradeClient
{
    public static class BloombergParser
    {
        public static RetrieveGetHistoryResponse ParseRetrieveHistoryResponse(string xml)
        {
            var serializer = new XmlSerializer(typeof(SoapEnvelope2));
            using var reader = new StringReader(xml);
            if (serializer.Deserialize(reader) is SoapEnvelope2 envelope)
            {
                return envelope.Body?.RetrieveGetHistoryResponse;
            }

            return null;
        }
    }
}
