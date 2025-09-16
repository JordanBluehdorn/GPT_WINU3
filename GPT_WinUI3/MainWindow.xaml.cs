using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;

using OpenAI;
using OpenAI.Chat;
using System.Text.Json;

namespace GPT_WinUI3
{
    public class MessageItem
    {
        public string Text { get; set; }
        public SolidColorBrush Color { get; set; }
    }

    public class ChatCommand
    {
        public string Message { get; set; }
        public string Action { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    public sealed partial class MainWindow : Window
    {
        private OpenAIClient openAiService;
        private bool smartChargingEnabled = false;

        public MainWindow()
        {
            this.InitializeComponent();

            var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

            openAiService = new(openAiKey);
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseProgressBar.Visibility = Visibility.Visible;
            string userInput = InputTextBox.Text;

            try
            {
                if (!string.IsNullOrEmpty(userInput))
                {
                    AddMessageToConversation($"User: {userInput}");
                    InputTextBox.Text = string.Empty;
                    var chatClient = openAiService.GetChatClient("gpt-4o"); // or another model
                    var chatOptions = new ChatCompletionOptions
                    {
                        MaxOutputTokenCount = 300
                    };

                    // Create your system prompt text
                    string systemPrompt = @"
                    You are a command parser for a Windows app.
                    When the user asks something, respond ONLY in JSON with this format:
                    {
                      ""Message"": ""<string>"",
                      ""Action"": ""<string>"",
                      ""Parameters"": { ... }
                    }

                    Supported actions:
                    - toggle_smart_charging: parameters { ""enabled"": true|false }
                    - get_smart_charging_status: parameters {}
                    Do not include any extra text outside the JSON.
                    Do not wrap the json codes in JSON markers.
                    Do not include line breaks or indentation.
                    ";


                    // Assemble the chat prompt with a system message and the user's input
                    var completionResult = await chatClient.CompleteChatAsync(
                        [
                            ChatMessage.CreateSystemMessage(systemPrompt),
                            ChatMessage.CreateUserMessage(userInput)
                        ],
                        chatOptions);

                    if (completionResult != null && completionResult.Value.Content.Count > 0)
                    {
                        string responseJson = completionResult.Value.Content[0].Text;

                        var command = JsonSerializer.Deserialize<ChatCommand>(responseJson);

                        switch(command?.Action)
                        {
                            case "toggle_smart_charging":
                                if (command.Parameters.TryGetValue("enabled", out var enabledObj) &&
                                    ((JsonElement)enabledObj).ValueKind == JsonValueKind.True)
                                {
                                    smartChargingEnabled = true;
                                    AddMessageToConversation("GPT: Smart charging has been enabled.");
                                }
                                else
                                {
                                    smartChargingEnabled = false;
                                    AddMessageToConversation("GPT: Smart charging has been disabled.");
                                }
                                break;
                            case "get_smart_charging_status":
                                AddMessageToConversation($"GPT: Smart charging is currently {(smartChargingEnabled ? "enabled" : "disabled")}.");
                                break;
                            default:
                                AddMessageToConversation($"GPT: {command?.Message}");
                                break;
                        }
                    }
                    else
                    {
                        AddMessageToConversation($"GPT: Sorry, something bad happened: {completionResult?.Value.Refusal ?? "Unknown error."}");
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessageToConversation($"GPT: Sorry, something bad happened: {ex.Message}");
            }
            finally
            {
                ResponseProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void AddMessageToConversation(string message)
        {
            var messageItem = new MessageItem
            {
                Text = message,
                Color = message.StartsWith("User:") ? new SolidColorBrush(Colors.LightBlue)
                                                    : new SolidColorBrush(Colors.LightGreen)
            };
            ConversationList.Items.Add(messageItem);

            // handle scrolling
            ConversationScrollViewer.UpdateLayout();
            ConversationScrollViewer.ChangeView(null, ConversationScrollViewer.ScrollableHeight, null);
        }

        private void InputTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter && !string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                SendButton_Click(this, new RoutedEventArgs());
            }
        }
    }
}