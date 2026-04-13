using Azure.AI.OpenAI;

namespace AnalyzeOrderLogs
{
    internal class ChatMessage : ChatRequestMessage
    {
        private ChatRole user;
        private string prompt;

        public ChatMessage(ChatRole user, string prompt)
        {
            this.user = user;
            this.prompt = prompt;
        }
    }
}