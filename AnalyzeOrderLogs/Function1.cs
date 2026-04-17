using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Monitor.Ingestion;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using ChatCompletionsOptions = Azure.AI.OpenAI.ChatCompletionsOptions;
using ChatRequestSystemMessage = Azure.AI.OpenAI.ChatRequestSystemMessage;
using ChatRequestUserMessage = Azure.AI.OpenAI.ChatRequestUserMessage;

namespace AnalyzeOrderLogs
{
    public static class AnalyzeOrderLogs
    {
        [FunctionName("AnalyzeOrderLogs")]
        public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req,
        ILogger log)
        {
            log.LogInformation("Info:Start function processed a request.");
            // ----------------------------
            // 1. Query Application Insights
            // ----------------------------

            var credential = new DefaultAzureCredential(
                            new DefaultAzureCredentialOptions
                            {
                                TenantId = "1991046d-90f4-4267-b457-28fea4f23b80"
                            });

            var logsClient = new LogsQueryClient(credential);

            string workspaceId = "730fa130-2bb8-4e17-9958-47b9d7d4fecd";

            string kqlQuery = @"
                            AppExceptions
                            | where TimeGenerated >= ago(3d)
                            | order by TimeGenerated desc
                            | take 1
                            ";


            try
            {
                // Run the query
                log.LogInformation("Info:Start Run the KQL query");
                Response<LogsQueryResult> response = await logsClient.QueryWorkspaceAsync(
                    workspaceId,
                    kqlQuery,
                    new QueryTimeRange(TimeSpan.FromDays(3)) // MUST match KQL//TimeSpan.FromHours(1) // Time range for query
                );

                log.LogInformation("Info:Run the KQL query successfully");

                var logsText = new StringBuilder();

                foreach (var row in response.Value.Table.Rows)
                {
                    logsText.AppendLine(string.Join(" | ", row));
                }
                log.LogInformation("logsText pepared successfully");

                //// ----------------------------
                //// 2. Read RAG context
                //// ----------------------------
                string contextPath = Path.Combine(AppContext.BaseDirectory, "architecture_context.txt");
                string architectureContext = File.Exists(contextPath)
                    ? File.ReadAllText(contextPath)
                    : "General OrderService architecture.";
                log.LogInformation("architecture_context file read successfully");
                ////-----------------------------

                //// ----------------------------
                //// 2. Build SRE Prompt
                //// ----------------------------
                string prompt = "You are an expert SRE. Analyze logs and return output ONLY in valid JSON format:\r\n\r\n{\r\n  \"incident_summary\": \"\",\r\n  \"root_cause\": \"\",\r\n  \"severity\": \"\",\r\n  \"next_steps\": \"\"\r\n}\r\n\r\nDo not include markdown, headings, or explanations outside JSON.";

                #region Using Azure Open AI
                //// ----------------------------
                //// 3. Call Azure OpenAI
                ////// ----------------------------

                // Retrieve the key from your local.settings.json environment variables
                string apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY");
                var client = new OpenAIClient(
                    new Uri("https://azure-mnylsmii-eastus2.cognitiveservices.azure.com/"),
                    new AzureKeyCredential(apiKey));

                // Configure options with your DEPLOYMENT name (not model name)
                var options = new ChatCompletionsOptions()
                {
                    DeploymentName = "gpt-4o", // Must match the name in Azure AI Studio
                    Messages = {
                        
                        ////-------Response without RAG---------
                        //new ChatRequestSystemMessage("You are an expert SRE. Analyze logs and return output ONLY in valid JSON format:\r\n\r\n{\r\n  \"incident_summary\": \"\",\r\n  \"root_cause\": \"\",\r\n  \"next_steps\": \"\"\r\n}\r\n\r\nDo not include markdown, headings, or explanations outside JSON."),
                        //---------Response with RAG-----------
                        new ChatRequestSystemMessage("You are an expert SRE. Analyze logs with  architectureContext "+ architectureContext+" and return output ONLY in valid JSON format:\r\n\r\n{\r\n  \"incident_summary\": \"\",\r\n  \"root_cause\": \"\",\r\n  \"severity\": \"\",\r\n  \"next_steps\": \"\"\r\n}\r\n\r\nDo not include markdown, headings, or explanations outside JSON."),
                        new ChatRequestUserMessage($"Analyze these service logs:\n{logsText}")

                    },
                    Temperature = 0.2f
                };

                log.LogInformation("Info:Prompt prepared and start calling Azure open AI");

                var responseAi = await client.GetChatCompletionsAsync(options); ;
                var summary = responseAi.Value.Choices[0].Message.Content.ToString();

                log.LogInformation("Info:Azure OpenAI Response recived successfully: {Summary}", summary);

                #endregion

                //// ----------------------------
                //// 4. Notification Alert
                ////// ----------------------------
                ///TODO: format the summary
                log.LogInformation("Info:Start Notification Alert");
                EmailSend(summary);
                log.LogInformation("Info:Notification alert send successfully");

                //// ----------------------------
                //// 5. send to work book
                ////// ----------------------------
                log.LogInformation("Info:Start PushToWorkBook");
                await PushToWorkBook(summary, log);

                log.LogInformation("Info:Return summary");
                return new OkObjectResult(responseAi.Value.Choices[0].Message.Content);
            }
            catch (Exception ex)
            {
                log.LogInformation($"Error querying logs: {ex.Message}");
                return new BadRequestObjectResult("Error querying logs.");
            }
        }


        /// <summary>
        /// email notification
        /// </summary>
        /// <param name="bodyData"></param>
        private static void EmailSend(string bodyData)
        {
            var fromAddress = new MailAddress("2010jainpragati@gmail.com", "Pragati Jain");
            var toAddress = new MailAddress("azuretest994@gmail.com");
            const string fromPassword = "vaeo dswj ryid jtim";

            var smtp = new SmtpClient
            {
                Host = "smtp.gmail.com",
                Port = 587,
                EnableSsl = true,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
            };

            StringBuilder tableRows = new StringBuilder();

            var incident = JsonConvert.DeserializeObject<IncidentModel>(bodyData);

            StringBuilder stepsHtml = new StringBuilder("<ul>");
            foreach (var step in incident.next_steps)
            {
                stepsHtml.Append($"<li>{step}</li>");
            }
            stepsHtml.Append("</ul>");

            string severityColor = "red";

            tableRows.Append($@"
    <tr>
        <td>{DateTimeOffset.UtcNow}</td>
        <td>{incident.incident_summary}</td>
        <td>{incident.root_cause}</td>
        <td>{stepsHtml}</td>
        <td style='color:{severityColor}; font-weight:bold;'>High</td>
    </tr>");

            string body = $@"
<html>
<body style='font-family: Arial;'>
    <table border='1' cellpadding='8' cellspacing='0' style='border-collapse: collapse; width:100%;'>
        <tr style='background-color:#f2f2f2;'>
            <th>Time Generated</th>
            <th>Incident Summary</th>
            <th>Root Cause</th>
            <th>Next Steps</th>
            <th>Severity</th>
        </tr>
        {tableRows}
    </table>
</body>
</html>";

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = "Incident Summary Of Exception",
                Body = body,
                IsBodyHtml = true
            })
            {
                smtp.Send(message);
            }
        }

        /// <summary>
        /// PushToWorkBook
        /// </summary>
        /// <param name="summary"></param>
        private static async Task PushToWorkBook(string summary, ILogger log)
        {
            var incident = JsonConvert.DeserializeObject<IncidentModel>(summary);

            var credential = new DefaultAzureCredential(
                                  new DefaultAzureCredentialOptions
                                  {
                                      TenantId = "1991046d-90f4-4267-b457-28fea4f23b80"
                                  });
            // 1. Initialize the client (Better to do this as a Singleton if possible)
            var endpoint = new Uri("https://myorderservicedce-3ifr.southindia-1.ingest.monitor.azure.com");
            var clientLogAnalytics = new LogsIngestionClient(endpoint, credential);

            var dcrImmutableId = "dcr-6842f8716a8b49f4b10c11501e89374f"; // From your DCR
            var streamName = "Custom-AI_Incidents_CL"; // Must start with Custom-

            // 2. Prepare the data payload
            var logData = new[]
            {
                new {
                    IncidentSummary = incident.incident_summary,
                    RootCause = incident.root_cause,
                    NextSteps = string.Join(", ", incident.next_steps), // Ensure it's a string or flat object
                    Severity = incident.severity,
                    TimeGenerated = DateTimeOffset.UtcNow
                }
                };

            // 3. Push to Log Analytics
            try
            {
                await clientLogAnalytics.UploadAsync(dcrImmutableId, streamName, logData);
                log.LogInformation($"Info:AI Analysis pushed to Log Analytics successfully");
            }
            catch (Exception ex)
            {
                log.LogInformation($"Info:Failed to push to Log Analytics: {ex.Message}");
            }

        }
    }

    /// <summary>
    /// model
    /// </summary>
    public class IncidentModel
    {
        public string incident_summary { get; set; }
        public string root_cause { get; set; }
        public string severity { get; set; }
        public List<string> next_steps { get; set; }
    }


}