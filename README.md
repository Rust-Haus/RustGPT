​Integrate AI conversations into your Rust game chat. Chat and image generation hooks available for developers.



Patch 1.8.1
+ Added support for image generation
Image generation hooks will only work with XAI and OpenAI. Anthropic does not support image generation.


What's New in 1.8.0

+ Multi-Provider Support: Choose between OpenAI, Anthropic, or XAI
+ Smart Provider Switching: Easily switch between providers with the /provider command
+ Enhanced System Prompts: Share server info and custom rules with your AI
+ Improved Message Handling: Better chunking and formatting for long responses
+ Discord Integration: Optional webhook support for chat messages (For Admin logging, not recommend for public)
+ Performance Optimized: Built-in cooldown system
- Death commentary removed. Will be implemented in a new plugin called AI Death Feed


Requirements
This plugin requires an API key from one of the following providers:
- OpenAI
- Anthropic
- XAI

Installation

Download RustGPT.cs
Place it in your server's oxide/plugins or carbon/plugins directory
Configure the plugin using the generated config file at ..config/RustGPT.json
Configuration

The plugin generates a default configuration with support for multiple AI providers:

{
  "AIProviders": {
    "openai": {
      "API Key": "your-api-key-here",
      "url": "https://api.openai.com/v1/chat/completions",
      "model": "gpt-4",
      "Max Tokens": 1500
    },
    "anthropic": {
      "API Key": "your-api-key-here",
      "url": "https://api.anthropic.com/v1/messages",
      "model": "claude-3-opus-20240229",
      "Max Tokens": 1500
    },
    "xai": {
      "API Key": "your-api-key-here",
      "url": "https://api.x.ai/v1/chat/completions",
      "model": "grok-1",
      "Max Tokens": 1500
    },
    "Active Provider": "OpenAI"
  },
  "AIPromptParameters": {
    "System role": "You are a helpful assistant on a Rust game server.",
    "User Custom Prompt": "Server wipes Thursdays at 2pm CST.",
    "Share Server Name": true,
    "Share Server Description": true,
    "Share Player Names": true,
    "AI Rules": []
  },
  "ChatSettings": {
    "Chat Message Color": "#FFFFFF",
    "Chat Message Font Size": 12,
    "Response Prefix": "[RustGPT]",
    "Response Prefix Color": "#55AAFF",
    "Question Pattern": "!gpt",
    "Chat cool down in seconds": 10,
    "Broadcast Response to the server": false
  }
}
 

Permissions

RustGPT.use - Allows players to use the chat command
RustGPT.admin - Grants access to admin commands and provider management
 
Commands

!gpt <message> - Send a message to the active AI provider
ProTip: You can set the trigger phrase for ai chat to nothing like this: "Question Pattern": "", and the RustGPT will respond to all global chat messages.

/provider [name] - Switch between available AI providers (requires RustGPT.admin)
 
Chat

Players can interact with the AI using the configured command (default: !gpt):

!gpt When does this server wipe?

LLM Provider Management

Admins can switch between providers using the /provider command:

/provider openai    # Switch to OpenAI
/provider anthropic # Switch to Anthropic
/provider xai       # Switch to XAI
 

Settings

1. API Keys: Configure your API keys for each provider you want to use
2. Active Provider: Choose your default AI provider. (Optional - you can just put add your API key to the config and RustGPT will use that provider.) 
3. System Prompt: Customize the AI's role and behavior
4. Chat Settings: Adjust message formatting and cooldown times

Optional Features

Discord Integration: 
Enable UseDiscordWebhookChat 
Add your Discord webhook URL (This is setup for admin logging. This webhook should point to a private discord channel.)
Custom Rules:
AI rules are attached to every message sent to the AI provider. 
 

Message Formatting

Customize colors using hex codes
Adjust font sizes for messages
Configure response prefix and colors
 

Support

For issues, questions, or contributions:

https://discord.gg/EQNPBxdjRu

 



​