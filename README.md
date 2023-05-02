# RustGPT Ver 1.6.5

Are you sick of playing Rust like a normal person? Yeah, us too. That's why we created the Rust GPT plugin, so you can chat with an AI while you wander aimlessly through the map, waiting to get shot out of nowhere. Who needs a game plan when you have an AI to talk to?

![Logo](https://i.imgur.com/KttasYy.png)

# Features 🔥

- Ask GPT questions directly from the game chat
- Get text-based answers from GPT
- Customizable cool down time
- Participate in the inevitable AI overlord takeover!
- Hooks for use in other plugins
- Now supports [**Discord Messages**](https://umod.org/plugins/discord-messages) and [**Death Notes**](https://umod.org/plugins/death-notes)

# Contributing

Contributions are always welcome! Reach out to Goo on Rust Haus' [discord]() if you want. Otherwise do a pull request and get crackin.

# Required OpenAI API Key

To run this project, you will need to get yourself a fancy [OpenAI API key](https://platform.openai.com/account/api-keys).

# Installation 🛠️

Copy the plugin `RustGPT.cs` into your Carbon or Oxide plugins folder.

When the plugin is first loaded it will generate a configuration file and the server console will say `Please configure the plugin with the required OpenAI API key.`  

Configure the plugin in the `RustGPT.json` file located in you Oxide or Carbon `configs` folder.

## config/RustGPT.json

```json
{
  "AIPromptParameters": {
    "System role": "You are a helpful assistant on Rust game server called Rust.Haus Testing Server.",
    "User Server Details": "Server wipes Thursdays at 2pm CST. Blueprints are wiped on forced wipes only. Gather rate is 5X. Available commands available by using /info. Server admin is Goo. The discord link is https://discord.gg/EQNPBxdjRu"
  },
  "AIResponseParameters": {
    "Frequency Penalty": 0.0,
    "Max Tokens": 150,
    "Model": "gpt-3.5-turbo",
    "Presence Penalty": 0.6,
    "Temperature": 0.9
  },
  "Broadcast Response to the server": false,
  "Chat cool down in seconds": 10,
  "OpenAI_Api_Key": {
    "OpenAI API Key": "your-api-key-here"
  },
  "OptionalPlugins": {
    "Broadcast RustGPT Messages to Discord?": false,
    "Death Notes GPT Prompt": "You are a color commentator on the hottest new deathmatch show of the future. You can use Markdown in your responses.",
    "DiscordMessages Webhook URL": "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
    "Turn on GPT Powered Death Notes": false
  },
  "OutboundAPIUrl": {
    "API URL": "https://api.openai.com/v1/chat/completions"
  },
  "Plugin Version": "1.6.6",
  "Question Pattern": "!gpt",
  "Response Prefix": "[RustGPT]",
  "Response Prefix Color": "#55AAFF"
}
```

To get movin' and groovin' make sure your API Key is in your `config/RustGPT.json`

Then assign permission to yourself or a group. This is how to grant permission to yourself.

In console for oxide:

` o.grant user <player_name_or_steam_id> RustGPT.use `

In console for Carbon:

` c.grant user <player_name_or_steam_id> RustGPT.use `

Restart the plugin.

- Using Oxide? Do this `o.reload RustGPT.cs`

- Using Carbon? Do this `c.reload RustGPT.cs`

**Do yourself a favor and make a local copy of your `RustGPT.json` file in case something goes wonky in future updates.**

# Configuration

## "AIResponseParameters" Explained

Since these parameters are part of the OpenAI API call payload, these are the links to the official documentation.
| Message Parameter      | Description |
| ----------- | ----------- |
| ["Model"](https://platform.openai.com/docs/api-reference/completions/create#completions/create-model)      | ID of the model to use. See the [model endpoint compatibility table](https://platform.openai.com/docs/models/model-endpoint-compatibility) for details on which models work with the Chat API.       |
| ["Temperature"](https://platform.openai.com/docs/api-reference/completions/create#completions/create-temperature)   | Optional. Defaults to 1. What sampling temperature to use, between 0 and 2. Higher values like 0.8 will make the output more random, while lower values like 0.2 will make it more focused and deterministic.        |
| ["Max Tokens"](https://platform.openai.com/docs/api-reference/completions/create#completions/create-max_tokens)   | The maximum number of [tokens](https://platform.openai.com/tokenizer) to generate in the chat completion. The total length of input tokens and generated tokens is limited by the model's context length.        |
| ["Presence Penalty"](https://platform.openai.com/docs/api-reference/completions/create#completions/create-presence_penalty)   | Number between -2.0 and 2.0. Positive values penalize new tokens based on whether they appear in the text so far, increasing the model's likelihood to talk about new topics. [See more information about frequency and presence penalties.](https://platform.openai.com/docs/api-reference/parameter-details)         |
|[frequency_penalty](https://platform.openai.com/docs/api-reference/completions/create#completions/create-frequency_penalty) |Number between -2.0 and 2.0. Positive values penalize new tokens based on their existing frequency in the text so far, decreasing the model's likelihood to repeat the same line verbatim. |

## "AIPromptParameters"  Explained

These parameters are a way to give you AI a head start on some answers. This will "pre-load" the conversations your players will have with the AI with information about your server and help creating a unique personality for the AI. These pre-loaded configurations will be sent at every API call so if you are worried about costs try and keep them as short as possible.

`"System role": "You are a helpful assistant on Rust game server called Rust.Haus Testing Server.",`

System Role is not always considered by the AI according to the OpenAI documentation. More info about this [here.](https://platform.openai.com/docs/guides/chat/introduction)

`"User Server Details": "Server wipes Thursdays at 2pm CST. Blueprints are wiped on forced wipes only. Gather rate is 5X. Available commands available by using /info. Server admin is Goo. The discord link is discord"`

![Usage example](https://i.imgur.com/UXoLsb4.png)

User Server Details is actually what you are programming the AI to think was a previous response to the player question "My name is `Player's Nam`. Tell me about this server." Loading this answer with as much data about the server as possible to give quick access to the AI.  

## Chat Cool Down

Setting this will try and prevent the API from being spammed into oblivion. Should also keep costs down.

![Cool down example](https://i.imgur.com/9Mip0gv.png)

# Usage/Examples

<!-- You can do this to have the AI answer some questions with a little more accuracy. 

```json
"GptAssistantIntro": "You are an assistant on the Rust game server called Rust Haus. The Rust Haus server wipes every Thursday around 2pm CST or if forced. Discord server at discord, website is https://rust.haus, Twitter is @HausRust81322.",
``` -->

## Question Patterns

This setting is designed to validate the chat message so only specified text triggers the API call.
Currently the default question structure is like this:

`"Question Pattern": "!gpt"`

This is an old method to trigger commands in rust. Basically, any chat message that contains the text "!gpt" will send that chat message through the API and trigger a response. You can edit this in the configuration file. Since this is capable of accepting regex expressions you can get crazy with it. For example:

`"Question Pattern": "(who|what|when|where|why|how|is|are|am|do|does|did|can|could|will|would|should|which|whom).*?$"`

This will look for a chat message that contains one of the various keywords and ends with a question mark.

`"Question Pattern": ""`

And incase you were wondering this will full send everything. Not recommended. If you have a well populated server and a lot of chat enthusiasts, this will cost you some money.

# Planned Updates

The main goal of this plugin is to eventually use it as a container for core AI functionality. The vision is to have the ability to use this as a way of implementing customized AI to your server. For example, a plugin that spawns in AI controlled player models that builds bases and raid others. However, before we get there we need to take care of some stuff...

- ~~Add the ability to switch between the Completions and the Chat endpoints. This will allow the use of GPT-3.5-Turbo. This will happen on the next update most likely.~~ Abandoned for this plugin. This plugin will only use gpt-3.5-turbo.

- Add token counting and alerts. Although the OpenAI API is fairly inexpensive it does have the ability to rack up some charges on a popular server.

## RustGPTHook

The `RustGPTHook` method is a hook for the RustGPT plugin that allows other plugins to make use of the GPT-3.5-turbo AI model from OpenAI.

### Usage

To use this hook, you need to pass the following parameters:

Parameters

- player: The BasePlayer object representing the player making the request.
- question: A string containing the question you want to ask the AI.
- apiKey: Your OpenAI API key.
- temperatureAI: A float value representing the AI's response randomness (between 0 and 1).
- maxTokens: An integer value representing the maximum number of tokens in the AI's response.
- systemRole: A string describing the role of the AI within the system.
- userServerDetails: A string containing information about the server.
- callback: An Action<string> delegate to handle the AI-generated response.

# Author

- [@purnellbp](https://www.github.com/purnellbp)

I'm not associated in any way with OpenAI or Facepunch but feel free to tell them how cool I am.

Huge thanks to Raul-Sorin Sorban for helping me getting this to work! He should be a co-author of the plugin but I am sparing him from the customer service requests. 😁

# Feedback

`goo@rust.haus`

[Github Discussions](https://github.com/Rust-Haus/RustGPT/discussions)

## License

MIT
