using Azure;
using Azure.AI.Inference;
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

namespace AnalyzeOrderLogs
{
    public static class AnalyzeOrderLogs
    {
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

        [FunctionName("AnalyzeOrderLogs")]
        public static async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequest req,
        ILogger log)
        {            
            // ----------------------------
            // 1. Query Application Insights
            // ----------------------------

            var credential = new DefaultAzureCredential(
                            new DefaultAzureCredentialOptions
                            {
                                TenantId = "bd87d415-3220-4383-94e8-b7596d428ba5"
                            });

            var logsClient = new LogsQueryClient(credential);

            string workspaceId = "08b8300d-886d-4133-9f27-13e2359eec27";

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
                //var client = new OpenAIClient(new Uri(Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")),
                //    new AzureKeyCredential(Environment.GetEnvironmentVariable("AZURE_OPENAI_KEY")));

                //var options = new ChatCompletionsOptions()
                //{
                //    DeploymentName = "gpt-4o-mini", // <-- set deployment here
                //   Messages = { new ChatMessage(ChatRole.User, prompt) } 
                //};

                //var response = await client.GetChatCompletionsAsync(options); 

                //Console.WriteLine(response.Value.Choices[0].Message.Content);

                //var summary = response.Value.Choices[0].Message.Content;

                #endregion

                //// ----------------------------
                //// 3. Call Git hub model
                //// ----------------------------
                #region Using Git hub Model
                // 1. Setup GitHub Model Client
                // Get your token: https://github.com
                string githubPat = "{githubPat}";
                var chatClient = new ChatCompletionsClient(
                    new Uri("https://models.github.ai/inference"),
                    new AzureKeyCredential(githubPat));


                //// ----------------------------
                //// 4. Craft the Prompt for AI Analysis
                //// ----------------------------
                var options = new Azure.AI.Inference.ChatCompletionsOptions()
                {
                    // Change DeploymentName to ModelName
                    Model = "gpt-4o",
                    Messages = {
                          new Azure.AI.Inference.ChatRequestSystemMessage("You are an expert SRE. Analyze logs to provide:" +
                          "1. Incident Summary, 2. Probable Root Cause, 3. Actionable Next Steps."),
                          new Azure.AI.Inference.ChatRequestUserMessage($"Analyze these service logs:\n{logsText}")
                    },
                    Temperature = 0.2f // Keep it concise and factual
                };
                //// ----------------------------
                //// 5. Get and Print the Analysis                
                //// ----------------------------
                Response<Azure.AI.Inference.ChatCompletions> chatResponse = await chatClient.CompleteAsync(options);
                Console.WriteLine("--- AI Analysis of Application Logs ---");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(chatResponse.Value.Content);
                Console.WriteLine(new string('-', 50));

                #endregion
                EmailSend(chatResponse.Value.Content);
                return new OkObjectResult(chatResponse.Value.Content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error querying logs: {ex.Message}");
                return new BadRequestObjectResult("Error querying logs.");
            }
        }
    }
}