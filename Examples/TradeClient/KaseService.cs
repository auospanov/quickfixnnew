using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
namespace TradeClient
{
    public class KaseService
    {
        public static async Task<string> tratement()
        {
            string symbol = "KZTK";
            string type = "shares"; // или "bonds"
            DateTime fromDate = DateTime.Today.AddDays(-10);
            DateTime toDate = DateTime.Today;

            string xmlRequest = GenerateSoapEnvelope(type, symbol, fromDate, toDate);

            string url = "http://iris.kase.kz:8080/iriscore/deals";
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(xmlRequest, Encoding.UTF8, "text/xml")
            };
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, errors) => true;
            // Basic auth
            string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("bccinvws1:nop31tnw"));
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

            using var client = new HttpClient(handler);
            try
            {
                var response = await client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Status: {response.StatusCode}");
                Console.WriteLine("Response:");
                Console.WriteLine(content);
                return content;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Request failed: " + ex.Message);
                return "";
            }
            return "";
        }

        public static string GenerateSoapEnvelope(string type, string symbol, DateTime from, DateTime to)
        {
            string methodName;
            if (type.ToLower() == "shares")
            {
                methodName = "getKaseDaySharesGenericForCode";
            }
            else if (type.ToLower() == "bonds")
            {
                methodName = "getKaseDayBondsGenericForCode";
            }
            else
            {
                throw new ArgumentException("Invalid type. Must be 'shares' or 'bonds'.");
            }

            return $@"<?xml version=""1.0"" encoding=""utf-8""?>
<soapenv:Envelope xmlns:soapenv=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:ws=""http://kz.irbis.ws/iris/deals"">
   <soapenv:Header/>
   <soapenv:Body>
      <ws:{methodName}>
         <arg0>{from.Year}</arg0>
         <arg1>{from.Month}</arg1>
         <arg2>{from.Day}</arg2>
         <arg3>{to.Year}</arg3>
         <arg4>{to.Month}</arg4>
         <arg5>{to.Day}</arg5>
         <arg6>0</arg6>
         <arg7>0</arg7>
         <arg8>0</arg8>
         <arg9>{symbol}</arg9>
      </ws:{methodName}>
   </soapenv:Body>
</soapenv:Envelope>";
        }
    }
}