# RustGPT Ver 1.7.7

Are you sick of playing Rust like a normal person? Yeah, us too. That's why we created the Rust GPT plugin, so you can chat with an AI while you wander aimlessly through the map, waiting to get shot out of nowhere. Who needs a game plan when you have an AI to talk to?

![Logo](https://i.imgur.com/KttasYy.png)


A powerful ChatGPT integration for Rust servers that enables AI-powered chat interactions and death commentary.

## Features

- 🤖 **AI Chat Integration**: Players can interact with ChatGPT directly in game chat
- 💀 **Death Commentary**: Hilarious AI-generated commentary for player deaths
- 🎨 **Customizable Formatting**: Configurable colors and font sizes for messages
- 🔄 **Smart Message Chunking**: Handles long responses with intelligent sentence splitting
- 🔌 **Discord Integration**: Optional webhook support for broadcasting chat and death messages
- ⚡ **Performance Optimized**: Built-in cooldown system and efficient API usage
- 🛡️ **Permission System**: Granular control over plugin features

## Installation

1. Download the latest release of `RustGPT.cs`
2. Place it in your server's `oxide/plugins` or `carbon/plugins` directory
3. Configure the plugin using the generated config file at `[oxide | carbon]/config/RustGPT.json`

## Configuration

The plugin will generate a default configuration file with the following structure:

```json
{
  "OpenAI_Api_Key": {
    "OpenAI API Key": "your-api-key-here"
  },
  "OutboundAPIUrl": {
    "API URL": "https://api.openai.com/v1/chat/completions"
  },
  "AIResponseParameters": {
    "Model": "gpt-4o-mini",
    "Temperature": 0.9,
    "Max Tokens": 1000,
    "Presence Penalty": 0.6,
    "Frequency Penalty": 0.2
  },
  "ChatSettings": {
    "Chat Message Color": "#FFFFFF",
    "Chat Message Font Size": 12
  },
  "DeathNoteSettings": {
    "Kill Message Color": "#ADD8E6",
    "Kill Message Font Size": 12,
    "Show simple kill feed in chat": false
  }
}
```

## Permissions

- `RustGPT.use` - Allows players to use the chat command
- `RustGPT.admin` - Grants access to admin commands and notifications

## Commands

- `!gpt <message>` - Send a message to ChatGPT (requires `RustGPT.use` permission)
- `/models` - List available OpenAI models (requires `RustGPT.admin` permission)

## Usage

### Chat Integration
Players can interact with ChatGPT by using the `!gpt` command:
```
!gpt What's the best way to raid a stone base?
```

### Death Commentary
When enabled, the plugin automatically generates witty commentary for player deaths. This feature can be toggled in the configuration.

## Configuration Guide

### Essential Settings

1. **API Key**: Replace `your-api-key-here` with your OpenAI API key
2. **Model**: Choose from available models (use `/models` command to list them)
3. **Response Parameters**: Adjust temperature and token limits to control AI behavior

### Optional Features

1. **Discord Integration**: 
   - Set `UseDiscordWebhookChat` to `true`
   - Add your Discord webhook URL
   
2. **Death Commentary**:
   - Set `UseDeathComment` to `true`
   - Customize the commentary prompt

### Message Formatting

- Customize colors using hex codes (e.g., "#FFFFFF")
- Adjust font sizes for different message types
- Configure response prefix and colors

## Support

For issues, questions, or contributions, please:
1. Check existing issues on GitHub
2. Create a new issue with detailed information
3. Join our Discord community for support

## License

This plugin is released under the MIT License. Feel free to modify and distribute as needed.

## Credits

Created by Goo_
Version: 1.7.7 