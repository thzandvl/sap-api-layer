using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace SAPAPILayer
{
    public static partial class APIRequest
    {
        
        public static async Task<APIResponse> GetAPIQuery(string url, string token, CookieCollection cookies, ILogger log)
        {
            // create a new API response object
            var apiresponse = new APIResponse();

            try
            {
                // create a new cookiecontainer for the OData request
                var cookieContainer = new CookieContainer();
                var handler = new HttpClientHandler();
                handler.CookieContainer = cookieContainer;

                // create a new http client for the OData request
                var client = new HttpClient(handler);
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("x-csrf-token", token);

                // add cookies from token request to the new OData request
                cookieContainer.Add(client.BaseAddress, cookies);

                // execute OData query
                log.LogInformation("Execute API query");
                HttpResponseMessage response = await client.GetAsync(client.BaseAddress);

                // process the response
                if (response.IsSuccessStatusCode)
                {
                    apiresponse = new APIResponse
                    {
                        StatusCode = (int)response.StatusCode,
                        Status = "Success",
                        Headers = "application/json",
                        Data = response.Content.ReadAsStringAsync().Result
                    };
                }
                else
                {
                    apiresponse = new APIResponse
                    {
                        StatusCode = (int)response.StatusCode,
                        Status = "Failed",
                        Headers = "application/json",
                        Error = "The query could not be executed, an error was returned",
                        Data = response.ToString()
                    };
                }

                return apiresponse;
            }
            catch (Exception ex) 
            {
                log.LogInformation("Error : " + ex.Message);
                apiresponse = new APIResponse
                {
                    StatusCode = 500,
                    Status = "Failed",
                    Headers = "application/json",
                    Error = "The query could not be executed, an error was returned",
                    Data = ex.Message
                };
                return apiresponse;
            }
        }

        public async static Task<(string, CookieCollection)> GetTokenCookies(string auth, ILogger log)
        {
            // get environment variables
            string sapurl = Environment.GetEnvironmentVariable("SAP_BASEURL", EnvironmentVariableTarget.Process);

            // create a new cookiecontainer for the token request
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler();
            handler.CookieContainer = cookieContainer;

            // create a new http client for the token request
            var client = new HttpClient(handler);
            client.BaseAddress = new Uri(sapurl);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("Authorization", auth);
            client.DefaultRequestHeaders.Add("x-csrf-token", "fetch");

            try
            {
                // execute the token request
                log.LogInformation("----------Get Token------------");
                log.LogInformation("Retrieve x-csrf-token");
                var response = await client.GetAsync(sapurl);

                string token = response.Headers.TryGetValues("x-csrf-token", out var values) ? values.FirstOrDefault() : null;
                log.LogInformation("x-csrf-token : " + token);
                log.LogInformation("-------------------------------");

                CookieCollection cookies = cookieContainer.GetCookies(new Uri(sapurl));

                log.LogInformation("----------Cookies------------");
                foreach(var cookie in  cookies)
                {
                    log.LogInformation(cookie.ToString());
                }
                log.LogInformation("-------------------------------");

                return (token, cookies);
            }
            catch(Exception ex)
            {
                log.LogInformation("Error : " + ex.Message);
                return (null, null);
            }
        }

        public static string ConvertDate(string value)
        {
            var dateString = (long)Convert.ToDouble(value.Substring(6, 13));
            DateTime date = DateTimeOffset.FromUnixTimeMilliseconds(dateString).DateTime;
            return date.ToString("yyyy-MM-dd");
        }

        public class APIResponse
        {
            public string Status { get; set; }

            public string Error { get; set; }

            public int StatusCode { get; set; }

            public string Headers { get; set; }

            public string Data { get; set; }
        }



    }

}
