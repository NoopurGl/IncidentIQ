using Azure;
using Azure.AI.Inference;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using ChatCompletions = Azure.AI.OpenAI.ChatCompletions;
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
            log.LogInformation("C# HTTP trigger function processed a request.");
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
                Response<LogsQueryResult> response = await logsClient.QueryWorkspaceAsync(
                    workspaceId,
                    kqlQuery,
                    new QueryTimeRange(TimeSpan.FromDays(3)) // MUST match KQL//TimeSpan.FromHours(1) // Time range for query
                );



                var logsText = new StringBuilder();

                foreach (var row in response.Value.Table.Rows)
                {
                    logsText.AppendLine(string.Join(" | ", row));
                }


                //// ----------------------------
                //// 2. Build SRE Prompt
                //// ----------------------------
                string prompt = $"""
                                You are an SRE assistant.

                                Summarize the following OrderService application logs:
                                - Identify the main issue
                                - Explain probable root cause
                                - Mention impacted service
                                - Suggest next steps

                                Logs:
                                {logsText}
                                """;


                #region Using Azure Open AI
                //// ----------------------------
                //// 3. Call Azure OpenAI
                ////// ----------------------------

                // Initialize the client

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
                        new ChatRequestSystemMessage("You are an expert SRE. Analyze logs and return output ONLY in valid JSON format:\r\n\r\n{\r\n  \"incident_summary\": \"\",\r\n  \"root_cause\": \"\",\r\n  \"next_steps\": \"\"\r\n}\r\n\r\nDo not include markdown, headings, or explanations outside JSON."),
                        new ChatRequestUserMessage($"Analyze these service logs:\n{logsText}")
                    },
                    Temperature = 0.2f
                };

                var  responseAi = await client.GetChatCompletionsAsync(options);
                //Console.WriteLine(responseAi.Value.Choices[0].Message.Content);     
                var summary = responseAi.Value.Choices[0].Message.Content.ToString();

                // Use _logger instead of Console.WriteLine
                log.LogInformation("OpenAI Response: {Summary}", summary);

                #endregion

                //// ----------------------------
                //// 3. Call Git hub model
                ////// ----------------------------
                #region Using Git hub Model
                //// 1. Setup GitHub Model Client
                //// Get your token: https://github.com
                //string githubPat = "{gitpat}";
                //var chatClient = new ChatCompletionsClient(
                //    new Uri("https://models.github.ai/inference"),
                //    new AzureKeyCredential(githubPat));


                ////// ----------------------------
                ////// 4. Craft the Prompt for AI Analysis
                ////// ----------------------------
                //var options = new Azure.AI.Inference.ChatCompletionsOptions()
                //{
                //    // Change DeploymentName to ModelName
                //    Model = "gpt-4o",
                //    Messages = {
                //          new ChatRequestSystemMessage("You are an expert SRE. Analyze logs and return output ONLY in valid JSON format:\r\n\r\n{\r\n  \"incident_summary\": \"\",\r\n  \"root_cause\": \"\",\r\n  \"next_steps\": \"\"\r\n}\r\n\r\nDo not include markdown, headings, or explanations outside JSON."),
                //          new Azure.AI.Inference.ChatRequestUserMessage($"Analyze these service logs:\n{logsText}")
                //    },
                //    Temperature = 0.2f // Keep it concise and factual
                //};
                ////// ----------------------------
                ////// 5. Get and Print the Analysis                
                ////// ----------------------------
                //Response<Azure.AI.Inference.ChatCompletions> chatResponseAi = await chatClient.CompleteAsync(options);
                //Console.WriteLine("--- AI Analysis of Application Logs ---");
                //Console.ForegroundColor = ConsoleColor.Green;
                //Console.WriteLine(chatResponseAi.Value.Content);
                //Console.WriteLine(new string('-', 50));

                #endregion

                //// ----------------------------
                //// 4. Notification Alert
                ////// ----------------------------
                ///TODO: format the summary
                EmailSend(summary);
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

            using (var message = new MailMessage(fromAddress, toAddress)
            {
                Subject = "Azure function",
                Body = bodyData
            })
            {
                smtp.Send(message);
            }
        }
    }
}