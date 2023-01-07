using AzureFunctions.Extensions.Swashbuckle.Attribute;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using static SAPAPILayer.APIRequest;
using System.Net;

namespace SAPAPILayer
{
    public static partial class APIRequest
    {
        /// <summary>
        /// Retrieve a single Purchase Order
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <remarks>
        /// If no number is set it will take the number of the first Purchase Order in the system
        /// </remarks>
        [QueryStringParameter("num", "Purchase Order Number", DataType = typeof(string), Required = false)]
        [FunctionName("GetPurchaseOrder")]
        public static async Task<IActionResult> GetPurchaseOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            // get environment variables
            string sapurl = Environment.GetEnvironmentVariable("SAP_BASEURL", EnvironmentVariableTarget.Process);

            log.LogInformation("-----Request Purchase Order----");

            // retrieve the JSON data
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogInformation("RequestBody : " + requestBody);

            // check for the authorization values
            string auth = req.Headers.Authorization;
            if (auth == null) return new UnauthorizedObjectResult(new APIResponse { Status = "Failed", StatusCode = 500, Error = "No Authorization header set" });

            // define the name for the API
            string num = String.IsNullOrEmpty(req.Query["num"]) ? "4500000001" : req.Query["num"];
            string apiname = "API_PURCHASEORDER_PROCESS_SRV/A_PurchaseOrder('" + num + "')";
            string reqURI = sapurl + apiname;
            log.LogInformation("OData URL : " + reqURI);

            // retrieve a token and cookies for the Purchase Order request --> Is this needed? No xcsrf token needed for a GET
            var (token, cookies) = GetTokenCookies(reqURI,auth, log).Result;
            if(token == null) return new UnauthorizedObjectResult(new APIResponse { Status = "Failed", StatusCode = 500, Error = "Token could not be retrieved" });

            // retrieve the Purchase Order
            log.LogInformation("Retrieve the Purchase Order");
            var response = GetAPIQuery(reqURI, token, cookies, log).Result;
            log.LogInformation("ResponseStatus : " + response.StatusCode);
            if(response.StatusCode != 200) return new NotFoundObjectResult(new APIResponse { Status = "Failed", StatusCode = response.StatusCode, Error = response.Error });

            JsonNode result = JsonNode.Parse(response.Data)!["d"]!;
            var poObj = JsonSerializer.Deserialize<PurchaseOrderObj>(result.ToString());

            // retrieve the Purchase Order Items
            log.LogInformation("Retrieve the Purchase Order Items");
            reqURI = reqURI + "/to_PurchaseOrderItem";
            response = GetAPIQuery(reqURI, token, cookies, log).Result;
            log.LogInformation("ResponseStatus : " + response.StatusCode);
            if (response.StatusCode != 200) return new NotFoundObjectResult(new APIResponse { Status = "Failed", StatusCode = response.StatusCode, Error = response.Error });

            // prepare a Purchase Order Object with Purchase Order Items
            log.LogInformation("Prepare Purchase Order object as response");
            var poItems = JsonNode.Parse(response.Data)!["d"]!["results"]!.AsArray();
            List <PurchaseOrderItemObj> items = new List<PurchaseOrderItemObj >();
            foreach (var poItem in poItems)
            {
                var itemNode = JsonSerializer.Deserialize<PurchaseOrderItemObj>(poItem.ToString());
                items.Add(itemNode);
            }
            poObj.Items = items;

            // return the result
            return new OkObjectResult(poObj);
        }


        /// <summary>
        /// Create a new Purchase Order
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <remarks>
        /// Post a JSON request in the following format:
        /// 
        /// 
        /// </remarks>
        /// <returns></returns>
        [FunctionName("CreatePurchaseOrder")]
        public static async Task<IActionResult> CreatePurchaseOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            // get environment variables
            string sapurl = Environment.GetEnvironmentVariable("SAP_BASEURL", EnvironmentVariableTarget.Process);

            log.LogInformation("-----Create Purchase Order-----");

            // retrieve the JSON data
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogInformation("RequestBody : " + requestBody);

            // check for the authorization values
            string auth = req.Headers.Authorization;
            if (auth == null) return new UnauthorizedObjectResult(new APIResponse { Status = "Failed", StatusCode = 500, Error = "No Authorization header set" });

            // define the name for the API
            string apiname = "API_PURCHASEORDER_PROCESS_SRV/A_PurchaseOrder";
            string reqURI = sapurl + apiname;
            log.LogInformation("OData URL : " + reqURI);

            // retrieve a token and cookies for the Purchase Order request
            var (token, cookies) = GetTokenCookies(reqURI, auth, log).Result;
            if (token == null) return new UnauthorizedObjectResult(new APIResponse { Status = "Failed", StatusCode = 500, Error = "XCSRF Token could not be retrieved" });

            // create a new Purchase Order
            log.LogInformation("Creating the Purchase Order");
            var postResponse = PostAPIQuery(reqURI, requestBody, token, cookies, log).Result;
            log.LogInformation("ResponseStatus : " + postResponse.StatusCode);
            if (postResponse.StatusCode != 200) return new NotFoundObjectResult(new APIResponse { Status = "Failed", StatusCode = postResponse.StatusCode, Error = postResponse.Error });
            JsonNode result = JsonNode.Parse(postResponse.Data)!["d"]!;
            var poObj = JsonSerializer.Deserialize<PurchaseOrderObj>(result.ToString());
            log.LogInformation($"Purchase Order {poObj.PurchaseOrder} successfully created");
            
            // prepare a Purchase Order Object with Purchase Order Items
            log.LogInformation("Prepare Purchase Order object as response");
            var poItems = JsonNode.Parse(postResponse.Data)!["d"]!["to_PurchaseOrderItem"]!["results"]!.AsArray();
            List<PurchaseOrderItemObj> items = new List<PurchaseOrderItemObj>();
            foreach (var poItem in poItems)
            {
                var itemNode = JsonSerializer.Deserialize<PurchaseOrderItemObj>(poItem.ToString());
                items.Add(itemNode);
            }
            poObj.Items = items;

            string reponseText = $"Purchase Order {poObj.PurchaseOrder} successfully created";
            PurchaseOrderCreateResponse poCreateResp = new PurchaseOrderCreateResponse();
            poCreateResp.reponseText = reponseText;
            poCreateResp.purchaseOrderObj = poObj;
            // return the result
            return new OkObjectResult(poCreateResp);

        }


        /// <summary>
        /// Retrieve a list of Purchase Orders
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("GetPurchaseOrderList")]
        public static async Task<IActionResult> GetPurchaseOrderList(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            // get environment variables
            string sapurl = Environment.GetEnvironmentVariable("SAP_BASEURL", EnvironmentVariableTarget.Process);

            log.LogInformation("-----Request Purchase Orders---");

            // retrieve the JSON data
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            string auth = req.Headers.Authorization;

            // check for the authorization values
            if (auth == null) return new UnauthorizedObjectResult(new APIResponse { Status = "Failed", StatusCode = 500, Error = "No Authorization header set" });

            log.LogInformation("RequestBody : " + requestBody);

            // define the name for the API
            string apiname = "API_PURCHASEORDER_PROCESS_SRV/A_PurchaseOrder";
            string reqURI = sapurl + apiname;

            log.LogInformation("OData URL : " + reqURI);

            // retrieve a Purchase Order token
            var (token, cookies) = GetTokenCookies(reqURI,auth, log).Result;
            if (token == null) return new UnauthorizedObjectResult(new APIResponse { Status = "Failed", StatusCode = 500, Error = "Token could not be retrieved" });

            // retrieve the Purchase Orders
            log.LogInformation("Retrieve the Purchase Order List");
            var response = GetAPIQuery(reqURI, token, cookies, log).Result;
            log.LogInformation("ResponseStatus : " + response.StatusCode);
            if (response.StatusCode != 200) return new NotFoundObjectResult(new APIResponse { Status = "Failed", StatusCode = response.StatusCode, Error = response.Error });

            var poItems = JsonNode.Parse(response.Data)!["d"]!["results"]!.AsArray();
            List<PurchaseOrderListItem> items = new List<PurchaseOrderListItem>();
            foreach (var poItem in poItems)
            {
                var itemNode = JsonSerializer.Deserialize<PurchaseOrderListItem>(poItem.ToString());
                items.Add(itemNode);
            }

            // return the result
            return new OkObjectResult(items);
        }

        public class PurchaseOrderCreateResponse
        {
            public string reponseText { get; set; }
            public PurchaseOrderObj purchaseOrderObj { get; set; }
        }

        public class PurchaseOrderObj
        {
            public string PurchaseOrder { get; set; }
            
            private string creationdate;

            public string PurchaseOrderType { get; set; }

            public string PurchasingOrganization { get; set; }

            public string PurchasingGroup { get; set; }

            public string AddressName { get; set; }

            public string Supplier { get; set; }

            public string DocumentCurrency { get; set; }

            public string CreatedByUser { get; set; }

            public string CreationDate { get => creationdate; set => creationdate = ConvertDate(value); }

            public List<PurchaseOrderItemObj> Items { get; set; }
        }


        public class PurchaseOrderItemObj
        {
            public string PurchaseOrderItem { get; set; }

            public string ManufacturerMaterial { get; set; }

            public string PurchaseOrderItemText { get; set; }

            public string Plant { get; set; }

            public string OrderQuantity { get; set; }

            public string OrderPriceUnit { get; set; }

            public string NetPriceAmount { get; set; }

            public string TotalPrice 
            { 
                get => (
                    float.Parse(NetPriceAmount, CultureInfo.InvariantCulture) * 
                    float.Parse(OrderQuantity, CultureInfo.InvariantCulture))
                    .ToString("f", CultureInfo.InvariantCulture
                    ); 
            }
        }


        public class PurchaseOrderListItem
        {
            private string creationdate;

            public string PurchaseOrderType { get; set; }

            public string AddressName { get; set; }

            public string CreationDate { get => creationdate; set => creationdate = ConvertDate(value); }

            public string PurchaseOrder { get; set; }
        }
    }
}
