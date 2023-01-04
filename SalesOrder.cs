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
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace SAPAPILayer
{
    public static partial class APIRequest
    {
        /// <summary>
        /// Retrieve a single Sales Order
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <remarks>
        /// If no number is set it will take the number of the first Sales Order in the system
        /// </remarks>
        [QueryStringParameter("num", "Sales Order Number", DataType = typeof(string), Required = false)]
        [FunctionName("GetSalesOrder")]
        public static async Task<IActionResult> GetSalesOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            // get environment variables
            string sapurl = Environment.GetEnvironmentVariable("SAP_BASEURL", EnvironmentVariableTarget.Process);

            log.LogInformation("-----Request Sales Order-------");

            // retrieve the JSON data
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogInformation("RequestBody : " + requestBody);

            // check for the authorization values
            string auth = req.Headers.Authorization;
            if (auth == null) return new UnauthorizedObjectResult(new APIResponse { Status = "Failed", StatusCode = 500, Error = "No Authorization header set" });

            // define the name for the API
            string num = String.IsNullOrEmpty(req.Query["num"]) ? "2" : req.Query["num"];
            string apiname = "API_SALES_ORDER_SRV/A_SalesOrder('" + num + "')";
            string reqURI = sapurl + apiname;
            log.LogInformation("OData URL : " + reqURI);

            // retrieve a token and cookies for the Sales Order request
            var (token, cookies) = GetTokenCookies(auth, log).Result;
            if (token == null) return new UnauthorizedObjectResult(new APIResponse { Status = "Failed", StatusCode = 500, Error = "Token could not be retrieved" });

            // retrieve the Sales Order Header
            log.LogInformation("Retrieve the Sales Order Header");
            var response = GetAPIQuery(reqURI, token, cookies, log).Result;
            log.LogInformation("ResponseStatus : " + response.StatusCode);
            if (response.StatusCode != 200) return new NotFoundObjectResult(new APIResponse { Status = "Failed", StatusCode = response.StatusCode, Error = response.Error });

            JsonNode result = JsonNode.Parse(response.Data)!["d"]!;
            var soObj = JsonSerializer.Deserialize<SalesOrderObj>(result.ToString());

            // retrieve the Sales Order Items
            log.LogInformation("Retrieve the Sales Order Items");
            reqURI = reqURI + "/to_Item";
            response = GetAPIQuery(reqURI, token, cookies, log).Result;
            log.LogInformation("ResponseStatus : " + response.StatusCode);
            if (response.StatusCode != 200) return new NotFoundObjectResult(new APIResponse { Status = "Failed", StatusCode = response.StatusCode, Error = response.Error });

            // prepare a Sales Order Object with Sales Order Items
            var soItems = JsonNode.Parse(response.Data)!["d"]!["results"]!.AsArray();
            List<SalesOrderItemObj> items = new List<SalesOrderItemObj>();
            foreach (var soItem in soItems)
            {
                var itemNode = JsonSerializer.Deserialize<SalesOrderItemObj>(soItem.ToString());
                items.Add(itemNode);
            }
            soObj.Items = items;

            // return the result
            return new OkObjectResult(soObj);
        }

        /// <summary>
        /// Retrieve a list of Sales Orders
        /// </summary>
        /// <param name="req"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        [FunctionName("GetSalesOrderList")]
        public static async Task<IActionResult> GetSalesOrderList(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)] HttpRequest req,
            ILogger log)
        {
            // get environment variables
            string sapurl = Environment.GetEnvironmentVariable("SAP_BASEURL", EnvironmentVariableTarget.Process);

            log.LogInformation("-----Request Sales Order List--");

            // retrieve the JSON data
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogInformation("RequestBody : " + requestBody);

            // check for the authorization values
            string auth = req.Headers.Authorization;
            if (auth == null) return new UnauthorizedObjectResult(new APIResponse { Status = "Failed", StatusCode = 500, Error = "No Authorization header set" });

            // define the name for the API
            string apiname = "API_SALES_ORDER_SRV/A_SalesOrder";
            string reqURI = sapurl + apiname;
            log.LogInformation("OData URL : " + reqURI);

            // retrieve a Sales Order token
            var (token, cookies) = GetTokenCookies(auth, log).Result;
            if (token == null) return new UnauthorizedObjectResult(new APIResponse { Status = "Failed", StatusCode = 500, Error = "Token could not be retrieved" });

            // retrieve the Sales Order List
            log.LogInformation("Retrieve the Sales Order List");
            var response = GetAPIQuery(reqURI, token, cookies, log).Result;
            log.LogInformation("ResponseStatus : " + response.StatusCode);
            if (response.StatusCode != 200) return new NotFoundObjectResult(new APIResponse { Status = "Failed", StatusCode = response.StatusCode, Error = response.Error });

            var soItems = JsonNode.Parse(response.Data)!["d"]!["results"]!.AsArray();
            List<SalesOrderListItem> items = new List<SalesOrderListItem>();
            foreach (var soItem in soItems)
            {
                var itemNode = JsonSerializer.Deserialize<SalesOrderListItem>(soItem.ToString());
                items.Add(itemNode);
            }

            // return the result
            return new OkObjectResult(items);
        }


        public class SalesOrderObj
        {
            public string deliverydate, creationdate;


            public string SalesOrder { get; set; }

            public string SalesOrderType { get; set; }

            public string CreatedByUser { get; set; }

            public string SoldToParty { get; set; }

            public string RequestedDeliveryDate { get => deliverydate; set => deliverydate = ConvertDate(value); }

            public string TotalNetAmount { get; set; }

            public string TransactionCurrency { get; set; }

            public string CreationDate { get => creationdate; set => creationdate = ConvertDate(value); }

            public string PurchaseOrderByCustomer { get; set; }

            public string PurchaseOrderByShipToParty { get; set; }

            public string SalesOrganization { get; set; }

            public string DistributionChannel { get; set; }

            public string OrganizationDivision { get; set; }

            public List<SalesOrderItemObj> Items { get; set; }
        }

        public class SalesOrderItemObj
        {
            public string SalesOrderItem { get; set; }

            public string Material { get; set; }

            public string SalesOrderItemText { get; set; }

            public string OrderQuantitySAPUnit { get; set; }

            public string ProductionPlant { get; set; }

            public string NetAmount { get; set; }

            public string RequestedQuantity { get; set; }

            public string UnitPrice 
            { 
                get => (
                    float.Parse(NetAmount, CultureInfo.InvariantCulture) / 
                    float.Parse(RequestedQuantity, CultureInfo.InvariantCulture))
                    .ToString("f", CultureInfo.InvariantCulture
                    ); 
            }
        }

        public class SalesOrderListItem
        {
            private string creationdate;

            public string SalesOrderType { get; set; }

            public string SoldToParty { get; set; }

            public string CreationDate { get => creationdate; set => creationdate = ConvertDate(value); }

            public string TotalNetAmount { get; set; }

            public string SalesOrder { get; set; }
        }

    }
}
