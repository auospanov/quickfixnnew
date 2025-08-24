using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace TradeClient
{
    public class BloombergSoapClient
    {
        public static async Task<string> RetrieveHistoryResponseAsync(string responseId)
        {
            var certPath = "bloom.p12";
            var certPassword = "Qwerty12";

            var certificate = new X509Certificate2(certPath, certPassword);

            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(certificate);
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) => true;

            using var client = new HttpClient(handler);

            var soapEnvelope = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ns=""http://services.bloomberg.com/datalicense/dlws/ps/20071001"">
   <soapenv:Header/>
   <soapenv:Body>
      <ns:retrieveGetHistoryRequest>
         <ns:responseId>{responseId}</ns:responseId>
      </ns:retrieveGetHistoryRequest>
   </soapenv:Body>
</soapenv:Envelope>";

            try
            {
                var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");

                // ОБЯЗАТЕЛЬНО: добавить SOAPAction (в кавычках!)
                content.Headers.Add("SOAPAction", "\"retrieveGetHistoryResponse\"");

                var url = "https://dlws.bloomberg.com/dlps";

                var response = await client.PostAsync(url, content);

                var responseString = await response.Content.ReadAsStringAsync();

                Console.WriteLine("Ответ Bloomberg:");
                Console.WriteLine(responseString);

                return responseString;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при получении данных: {ex.Message}");
                return null;
            }
        }



        public static async Task SendSoapRequestAsync()
        {
            var certPath = "bloom.p12"; // путь к сертификату клиента
            var certPassword = "Qwerty12";

            var certificate = new X509Certificate2(certPath, certPassword);

            var handler = new HttpClientHandler();
            handler.ClientCertificates.Add(certificate);
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) => true;

            using var client = new HttpClient(handler);

            var soapEnvelope = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ns=""http://services.bloomberg.com/datalicense/dlws/ps/20071001"">
   <soapenv:Header/>
   <soapenv:Body>
      <ns:submitGetHistoryRequest>
         <ns:headers>
            <ns:requestId>1</ns:requestId>
            <ns:priority>0</ns:priority>
            <ns:clientId>MyClient</ns:clientId>
            <ns:useUTC>true</ns:useUTC>
         </ns:headers>
         <ns:fields>
            <ns:field>PX_LAST</ns:field>
            <ns:field>PX_OPEN</ns:field>
         </ns:fields>
         <ns:instruments>
            <ns:instrument>
               <ns:type>ISIN</ns:type>
               <ns:id>US0378331005</ns:id>
            </ns:instrument>
         </ns:instruments>
         <ns:dateRange>
            <ns:startDate>20250419</ns:startDate>
            <ns:endDate>20250429</ns:endDate>
         </ns:dateRange>
      </ns:submitGetHistoryRequest>
   </soapenv:Body>
</soapenv:Envelope>";

            var content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");

            // ОБЯЗАТЕЛЬНО: добавить SOAPAction (в кавычках!)
            content.Headers.Add("SOAPAction", "\"submitGetHistoryRequest\"");

            var url = "https://dlws.bloomberg.com/dlps";

            try
            {
                var response = await client.PostAsync(url, content);
                var responseString = await response.Content.ReadAsStringAsync();

                Console.WriteLine("Ответ от Bloomberg:");
                Console.WriteLine(responseString);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }
        public static SubmitGetHistoryResponse ParseHistoryResponse(string xml)
        {
            var serializer = new XmlSerializer(typeof(SoapEnvelope));
            using var reader = new StringReader(xml);
            var envelope = (SoapEnvelope)serializer.Deserialize(reader);
            return envelope?.Body?.SubmitGetHistoryResponse;
        }
        /*
         <?xml version="1.0" encoding="UTF-8" ?><soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/"><soap:Body><dlws:submitGetHistoryResponse xmlns='http://services.bloomberg.com/datalicense/dlws/ps/20071001' xmlns:dlws="http://services.bloomberg.com/datalicense/dlws/ps/20071001" xmlns:env="http://schemas.xmlsoap.org/soap/envelope/"><dlws:statusCode><dlws:code>0</dlws:code><dlws:description>Success</dlws:description></dlws:statusCode><dlws:requestId>4804a8c6-ec8e-43f9-96f1-02fa1de4a9a9</dlws:requestId><dlws:responseId>1745944921-1970589519</dlws:responseId></dlws:submitGetHistoryResponse></soap:Body></soap:Envelope>
         */
    }
}
//date_from=2025date_fromN=4date_fromJ=19dateY=2025dateN=4dateJ=29=0=0=0=KZTO