
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.AI;
using OpenAI;

namespace MCPValidation
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var schemaPath = "/Users/robertwoodley/code/GreenAcres/ConversationalUX/bin/Debug/net9.0/enhancedSchema.json";
            var systemPromptPath = "/Users/robertwoodley/code/GreenAcres/Agent/prompts/SystemPrompt.txt";
            var userPromptPath = "/Users/robertwoodley/code/GreenAcres/ConversationalUX/evals/Case-03/prompt.txt";
            var toolRequestPath = args[0];
            // "/Users/robertwoodley/code/GreenAcres/ConversationalUX/evals/Case-03/run-250806-151326/0e744688-db3d-4ec1-b249-a773b0e2b4ca/19f441e6-a86b-485e-9305-917a7bfbe8e4/request-f97620ad-d9f5-4f78-9020-2050d6760ee7.json";
            var modelName = "gpt-4.1";
            Console.WriteLine($"Validating tool request: {toolRequestPath} using {modelName}");
            var schemaJson = File.ReadAllText(schemaPath);
            var systemPrompt = File.ReadAllText(systemPromptPath);
            var userPrompt = File.ReadAllText(userPromptPath);
            var toolRequestJson = File.ReadAllText(toolRequestPath);

            var prompt = $"""
                Today's date is {DateTime.UtcNow:yyyy-MM-dd}.
                You are an expert in validating structured tool requests against user prompts, system instructions, and schemas.
                Evaluate whether the following MCP tool request was generated correctly.

                System Prompt:
                {systemPrompt}

                User Prompt:
                {userPrompt}

                Tool Request:
                {toolRequestJson}

                Schema:
                {schemaJson}

                Your evaluation must:
                - Check compliance with all system prompt rules.
                - Validate that all required schema fields are present and correctly filled.
                - Confirm defaults are correctly applied.
                - Identify any mismatches with user-provided information. Ignore mismatches solely due to case sensitivity.
                - Report any issues and suggest fixes if needed. Only report problems, don't report on things that are correct.

                The final line of your response must be either "TOOL REQUEST VALID" or "TOOL REQUEST INVALID".
            """;
            // Console.WriteLine($"--- MCP Tool Request Validation Report via {modelName} ---\n");

            var openAIChatClient = new OpenAIClient(GetOpenAiKey()).GetChatClient(modelName);
            IChatClient chatClient = openAIChatClient.AsIChatClient();
            List<ChatMessage> chatHistory =
                [
                    new ChatMessage(ChatRole.System, prompt)
                ];
            bool done = false;
            bool firstRun = true;
            string response = "";
            bool isInteractive = args.Length > 1 && args[1] == "interactive";
            while (!done)
            {
                // Get user prompt and add to chat history
                if (!firstRun)
                {
                    Console.WriteLine("Press Enter to continue or type 'exit' to quit.");
                    string? input = Console.ReadLine();
                    if (input?.Trim().ToLower() == "exit")
                    {
                        done = true;
                        continue;
                    }
                    chatHistory.Add(new ChatMessage(ChatRole.User, input ?? ""));
                }
                firstRun = false;
                done = !isInteractive;

                // Stream the AI response and add to chat history
                if (isInteractive) Console.WriteLine("AI Response:");
                response = "";
                await foreach (ChatResponseUpdate item in
                    chatClient.GetStreamingResponseAsync(chatHistory))
                {
                    // Console.Write(item.Text);
                    response += item.Text;
                }
                chatHistory.Add(new ChatMessage(ChatRole.Assistant, response));
            }
            if (!isInteractive)
            {
                var validationFilePath = Path.Combine(
                        Path.GetDirectoryName(toolRequestPath) ?? "",
                        "validation-report.txt"
                    );

                var status = response.Contains("TOOL REQUEST VALID") ? "Valid" : "Invalid";

                File.WriteAllText(validationFilePath, response);

                if (!File.Exists("validation-report.html"))
                {
                    File.WriteAllText("validation-report.html",
                        "<html><head><title>Validation Report</title></head><body>" +
                        "<h1>Validation Report</h1>" +
                        "<table border='1'><tr><th>Date</th><th>Tool Request</th><th>Status</th></tr>"
                        + Environment.NewLine);
                }

                File.AppendAllText("validation-report.html",
                $"<tr><td>{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}</td>" +
                $"<td><a href=\"{toolRequestPath}\">{Path.GetFileName(toolRequestPath)}</a></td>" +
                $"<td><a href=\"{validationFilePath}\">{status}</a></td>" +
                $"</tr>" + Environment.NewLine
                );
            }
        }
        internal static string GetOpenAiKey()
        {
            string key;
            var keyPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openai", "key");
            if (File.Exists(keyPath))
            {
                key = File.ReadAllText(keyPath).Trim();
                if (string.IsNullOrEmpty(key))
                    throw new InvalidOperationException("OpenAI API key file is empty.");
            }
            else
                throw new FileNotFoundException("OpenAI API key file not found at " + keyPath);
            return key;
        }
    }
}
