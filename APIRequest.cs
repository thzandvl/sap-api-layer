using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static SAPAPILayer.APIRequest;

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

        public async static Task<(string, CookieCollection)> GetTokenCookies(string url, string auth, ILogger log)
        {
            // get environment variables
            // string sapurl = Environment.GetEnvironmentVariable("SAP_BASEURL", EnvironmentVariableTarget.Process);
            // create a new cookiecontainer for the token request
            var cookieContainer = new CookieContainer();
            var handler = new HttpClientHandler();
            handler.CookieContainer = cookieContainer;

            // create a new http client for the token request
            var client = new HttpClient(handler);
            client.BaseAddress = new Uri(url);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Add("Authorization", auth);
            client.DefaultRequestHeaders.Add("x-csrf-token", "fetch");
            
            try
            {
                // execute the token request
                log.LogInformation("----------Get Token------------");
                log.LogInformation("Retrieve x-csrf-token");

                //Execute a GET
                //var response = await client.GetAsync(sapurl);
                //Execute a HEAD
                log.LogInformation("HEAD Url : " + url);
                var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, url));

                string token = response.Headers.TryGetValues("x-csrf-token", out var values) ? values.FirstOrDefault() : null;
                log.LogInformation("x-csrf-token : " + token);
                log.LogInformation("-------------------------------");

                CookieCollection cookies = cookieContainer.GetCookies(new Uri(url));

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

        public static async Task<APIResponse> PostAPIQuery(string url, string content, string token, CookieCollection cookies, ILogger log)
        {
            log.LogInformation($"Start Post API query: {url}");
            log.LogInformation($"PostContent : {content}");
            // create a new Purchase Order
            var apiResponse = new APIResponse();
            try
            {
                var handler = new HttpClientHandler();
                var cookieContainer = new CookieContainer();
                handler.CookieContainer = cookieContainer;
                var client = new HttpClient(handler);
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                //client.DefaultRequestHeaders.Add("x-csrf-token", token); --> moved to postContent

                // add cookies from token request to the new OData request
                cookieContainer.Add(client.BaseAddress, cookies);

                log.LogInformation("--- Execute Post ... ");
                var postContent = new StringContent(content);
                postContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                postContent.Headers.Add("x-csrf-token", token);
                HttpResponseMessage response = await client.PostAsync(url, postContent);
                log.LogInformation("--- ... Post Executed");
                log.LogInformation("--- Response Code : " + response.StatusCode);

                //process the Response
                if (response.IsSuccessStatusCode)
                {
                    apiResponse = new APIResponse
                    {
                        //StatusCode = (int)response.StatusCode,
                        StatusCode = 200,
                        Status = "Success",
                        Headers = "application/json",
                        Data = response.Content.ReadAsStringAsync().Result
                    };
                }
                else
                {
                    apiResponse = new APIResponse
                    {
                        StatusCode = (int)response.StatusCode,
                        Status = "Failed",
                        Headers = "application/json",
                        Error = "The post could not be executed, an error was returned",
                        Data = response.ToString()
                    };
                }

                return apiResponse;

            }
            catch (Exception ex)
            {
                log.LogInformation("... Error During Post");
                log.LogInformation("Error : " + ex.Message);
                apiResponse = new APIResponse
                {
                    StatusCode = 500,
                    Status = "Failed",
                    Headers = "application/json",
                    Error = "The post could not be executed, an error was returned",
                    Data = ex.Message
                };
            }
            log.LogInformation("... End Post API query");
            return apiResponse;
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
