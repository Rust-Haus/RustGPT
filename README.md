
 Are you sick of playing Rust like a normal person? Yeah, us too. That's why we created the Rust GPT plugin, so you can chat with an AI while you wander aimlessly through the map, waiting to get shot out of nowhere. Who needs a game plan when you have an AI to talk to?

## **You should read this entire README if you are using this plugin for the first time.**

  
![Logo](https://i.imgur.com/KttasYy.png)


# Version 1.6.1

# Features 🔥

- Ask GPT questions directly from the game chat

- Get text-based answers from GPT
<!-- - Customizable cool down time -->
- Participate in the inevitable AI overlord takeover!

# Contributing

Contributions are always welcome! Reach out to Goo on Rust Haus' [discord](https://discord.gg/EQNPBxdjRu) if you want. Otherwise do a pull request and get crackin.



# Required OpenAI API Key

To run this project, you will need to get yourself a fancy [OpenAI API key](https://platform.openai.com/account/api-keys).


# Installation 🛠️


Copy the plugin `RustGPT.cs` into your Carbon or Oxide plugins folder.

When the plugin is first loaded it will generate a configuration file and the server console will say `Please configure the plugin with the required OpenAI API key.`  

Configure the plugin in the `RustGPT.json` file located in you Oxide or Carbon `configs` folder. 

As of ver 1.6.1 the config file looks like this:

```json
{
  "AIPromptParameters": {
    "System role": "You are a helpful assistant on Rust game server called Rust.Haus Testing Server.",
    "User Server Details": "Server wipes Thursdays at 2pm CST. Blueprints are wiped on forced wipes only. Gather rate is 5X. Available commands available by using /info. Server admin is Goo. The discord link is https://discord.gg/EQNPBxdjRu"
  },
  "AIResponseParameters": {
    "Frequency Penalty": 0.0,
    "Max Tokens": 150,
    "Presence Penalty": 0.6,
    "Temperature": 0.9
  },
  "Broadcast Response to the server": false,
  "Chat cool down in seconds": 10,
  "OpenAI_Api_Key": {
    "OpenAI API Key": "your-api-key-here" // Get this API key here https://platform.openai.com/account/api-keys
  },
  "OutboundAPIUrl": {
    "API URL": "https://api.openai.com/v1/chat/completions"
  },
  "Plugin Version": "1.6.1",
  "Question Pattern": "!gpt",
  "Response Prefix": "[RustGPT]",
  "Response Prefix Color": "#55AAFF"
}
```    

To get movin' and groovin' make sure you API Key is in your `config/RustGPT.json` 

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

["Max Tokens"](https://platform.openai.com/docs/api-reference/completions/create#completions/create-max_tokens) 

["Presence Penalty"](https://platform.openai.com/docs/api-reference/completions/create#completions/create-presence_penalty)

["Temperature"](https://platform.openai.com/docs/api-reference/completions/create#completions/create-temperature)

~~["Model"](https://platform.openai.com/docs/api-reference/completions/create#completions/create-model)~~ Removed in 1.6.1 - Model is now hardcoded to use `"model": "gpt-3.5-turbo"` This requires a paid API key that costs $0.002 / 1K tokens. This is 1/10th the cost of the previous model used. 

## "AIPromptParameters"  Explained

These parameters are a way to give you AI a head start on some answers. This will "pre-load" the conversations your players will have with the AI with information about your server and help creating a unique personality for the AI. These pre-loaded configurations will be sent at every API call so if you are worried about costs try and keep them as short as possible.

`"System role": "You are a helpful assistant on Rust game server called Rust.Haus Testing Server.",`

System Role is not always considered by the AI according to the OpenAI documentation. More info about this [here.](https://platform.openai.com/docs/guides/chat/introduction)

`"User Server Details": "Server wipes Thursdays at 2pm CST. Blueprints are wiped on forced wipes only. Gather rate is 5X. Available commands available by using /info. Server admin is Goo. The discord link is https://discord.gg/EQNPBxdjRu"`

![Usage example](https://i.imgur.com/UXoLsb4.png)

User Server Details is actually what you are programming the AI to think was a previous response to the player question "My name is <Player's Name>. Tell me about this server." Loading this answer with as much data about the server as possible to give quick access to the AI.  

## Chat Cool Down

Setting this will try and prevent the API from being spammed into oblivion. Should also keep costs down. 

![Cool down example](https://i.imgur.com/9Mip0gv.png)
 
# Usage/Examples

<!-- You can do this to have the AI answer some questions with a little more accuracy. 

```json
"GptAssistantIntro": "You are an assistant on the Rust game server called Rust Haus. The Rust Haus server wipes every Thursday around 2pm CST or if forced. Discord server at https://discord.gg/EQNPBxdjRu, website is https://rust.haus, Twitter is @HausRust81322.",
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

# Commands

For convenience there is the chat command `/askgpt` Where you can input directly to the AI without worrying about question validation. Don't try and train the AI this way it won't work...yet.



# Planned Updates

The main goal of this plugin is to eventually use it as a container for core AI functionality. The vision is to have the ability to use this as a way of implementing customized AI to your server. For example, a plugin that spawns in AI controlled player models that builds bases and raid others. However, before we get there we need to take care of some stuff... 

- Add the GPT-4 API payload to the configuration for you lucky devils with beta access. 

- ~~Add the ability to switch between the Completions and the Chat endpoints. This will allow the use of GPT-3.5-Turbo. This will happen on the next update most likely.~~ Abandoned for this plugin. This plugin will only use gpt-3.5-turbo.

- Multi-language support: Detect the language of the user's question and set the appropriate language model for the ChatGPT API request. This can provide a more inclusive experience for players who speak different languages.

- Adaptive learning: Monitor players' interactions with the AI and analyze the most common topics or questions. Use this information to fine-tune the AI model, pre-cache responses, or implement a custom knowledge base for better user experience.

- ~~Throttling AI usage: Introduce a configurable cool down timer for the AI responses. This prevents players from spamming questions and makes the AI interaction feel more natural, as if they are communicating with another player. Oxide didn't like this when on the first attempt to make this happen.~~ Implemented in 1.6.1 

- Add token counting and alerts. Although the OpenAI API is fairly inexpensive it does have the ability to rack up some charges on a popular. (Will be added next update.)

- Publicly expose core methods for use in other plugins. Specifically, an AI Agent plugin already in work. This will allow the RustGPT to assign other AI instances to more time consuming projects like monitoring a player, performing audits, building a base, raiding a base, etc. 



# AskRustGPT Hook

This method sends a question to the OpenAI GPT-3.5 Turbo API and processes the response.

#### Parameters

- `BasePlayer player`: The player who asks the question.
- `string question`: The question to ask the AI.
- `Action<string> callback`: A callback function that will be called with the AI's response as a parameter.
- `bool broadcastResponse`: A boolean flag indicating whether the AI response should be broadcasted to all players or sent privately to the asking player.

#### Usage

To use this method in another plugin, you can call it like this:

```csharp
yourPluginInstance.AskRustGPT(player, question, (response) => {
    // Do something with the response, e.g., display it in the chat
}, broadcastResponse); 
```
Make sure to replace yourPluginInstance with the actual instance of the RustGPT plugin. You can obtain the instance by subscribing to the plugin's hook, OnRustGPTLoaded.

### Example

Here's an example of how to use the AskRustGPT method in another plugin:
```csharp
[PluginReference]
Plugin RustGPT;

void OnRustGPTLoaded()
{
    Puts("RustGPT plugin loaded.");
}

void OnPlayerChat(BasePlayer player, string chatQuestion)
{
    if (chatQuestion.StartsWith("/askai "))
    {
        string question = chatQuestion.Substring(7);

        RustGPT?.Call("AskRustGPT", player, question, (Action<string>)(response => {
            player.ChatMessage($"<color=green>AI:</color> {response}");
        }), false);
    }
}
```

# Author

- [@purnellbp](https://www.github.com/purnellbp)

I'm not associated in any way with OpenAI or Facepunch but feel free to tell them how cool I am. 

Huge thanks to [Raul-Sorin Sorban](https://codefling.com/raul-sorin-sorban) for helping me getting this to work! He should be a co-author of the plugin but I am sparing him from the customer service requests. 😁 


# Feedback

`goo@rust.haus`

[Rust Haus Discord](https://discord.gg/QXhG7KRH)

[Github Discussions](https://github.com/Rust-Haus/RustGPT/discussions)



## License

[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://choosealicense.com/licenses/mit/)

![Cool down example](https://i.imgur.com/1cT2nGt.png)