# Rust GPT 🤖

  

Are you sick of playing Rust like a normal person? Yeah, us too. That's why we created the Rust GPT plugin, so you can chat with an AI while you wander aimlessly through the map, waiting to get shot out of nowhere. Who needs a game plan when you have an AI to talk to?

  
![Logo](https://i.imgur.com/KttasYy.png)




# Features 🔥


- Ask GPT questions directly from the game chat
- Get text-based answers from GPT
- Customizable cool down time
- Participate in the inevitable AI overlord takeover!



# Contributing

Contributions are always welcome! Reach out to Goo on Rust Haus' [discord](https://discord.gg/EQNPBxdjRu) if you want. Otherwise do a pull request and get crackin.



# Required OpenAI API Key

To run this project, you will need to get yourself a fancy [OpenAI API key](https://platform.openai.com/account/api-keys).



# Installation 🛠️


Copy the plugin `RustGPT.cs` into your Carbon or Oxide plugins folder.

When the plugin is first loaded it will generate a configuration file and the server console will say `Please configure the plugin with the required OpenAI API key.`  

Configure the plugin in the `RustGPT.json` file located in you Oxide or Carbon `configs` folder. 

As of ver 1.6 the config file looks like this:

```json
{
  "AIResponseParameters": {
    "Frequency Penalty": 0.0,
    "Max Tokens": 150,
    "Model": "text-davinci-003",
    "Presence Penalty": 0.6,
    "Temperature": 0.9
  },
  "OpenAI_Api_Key": {
    "API Key": "your-api-key-here"
  },
  "OutboundAPIUrl": {
    "API URL": "https://api.openai.com/v1/completions"
  },
  "Question Pattern": "!gpt",
  "Response Prefix": "[RustGPT]",
  "Response Prefix Color": "#55AAFF"
}
```    

To get movin' and groovin' just paste in your OpenAI API key and restart the plugin.

- Hop in to the console and restart the plugin. 

- Using Oxide? Do this `o.reload RustGPT.cs`

- Using Carbon? Do this `c.reload RustGPT.cs`

**Do yourself a favor and make a local copy of your `RustGPT.json` file in case something goes wonky in future updates.**

## Permissions added in 1.6
Since cool downs have been removed for...reasons, a permission has been added so that your entire server doesn't kill your OpenAI account.

In console for oxide:

` o.grant user <player_name_or_steam_id> ChatGPT.use `

In console for Carbon:

` c.grant user <player_name_or_steam_id> ChatGPT.use `

# Configuration

## "AIResponseParameters" Explained

Since these parameters are part of the OpenAI API call payload, these are the links to the official documentation.

["Max Tokens"](https://platform.openai.com/docs/api-reference/completions/create#completions/create-max_tokens) 

["Presence Penalty"](https://platform.openai.com/docs/api-reference/completions/create#completions/create-presence_penalty)

["Temperature"](https://platform.openai.com/docs/api-reference/completions/create#completions/create-temperature)

["Model"](https://platform.openai.com/docs/api-reference/completions/create#completions/create-model)
 


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

`"Question Pattern": "\b(who|what|when|where|why|how|is|are|am|do|does|did|can|could|will|would|should|which|whom)\b.*\?$"`

This will look for a chat message that contains one of the various keywords and ends with a question mark.

`"Question Pattern": ""`

And incase you were wondering this will full send everything. Not recommended. If you have a well populated server and a lot of chat enthusiasts, this will cost you some money. 

# Commands

For convenience there is the chat command `/askgpt` Where you can input directly to the AI without worrying about question validation. Don't try and train the AI this way it won't work...yet.



# Roadmap

The main goal of this plugin is to eventually use it as a container for core AI functionality. The vision is to have the ability to use this as a way of implementing customized AI to your server. For example, a plugin that spawns in AI controlled player models that builds bases and raid others. However, before we get there we need to take care of some stuff... 

- Add the GPT-4 API payload to the configuration for you lucky devils with beta access. 

- Add the ability to switch between the Completions and the Chat endpoints. This will allow the use of GPT-3.5-Turbo. This will happen on the next update most likely. 

- Multi-language support: Detect the language of the user's question and set the appropriate language model for the ChatGPT API request. This can provide a more inclusive experience for players who speak different languages.

- Adaptive learning: Monitor players' interactions with the AI and analyze the most common topics or questions. Use this information to fine-tune the AI model, pre-cache responses, or implement a custom knowledge base for better user experience.

- Throttling AI usage: Introduce a configurable cool down timer for the AI responses. This prevents players from spamming questions and makes the AI interaction feel more natural, as if they are communicating with another player. Oxide didn't like this when on the first attempt to make this happen. 

- Add token counting and alerts. Although the OpenAI API is fairly inexpensive it does have the ability to rack up some charges on a popular.

- Publicly expose core methods for use in other plugins. Specifically, an AI Agent plugin already in work. This will allow the RustGPT to assign other AI instances to more time consuming projects like monitoring a player, performing audits, building a base, raiding a base, etc. 

# Author

- [@purnellbp](https://www.github.com/purnellbp)

I'm not associated in any way with OpenAI or Facepunch but feel free to tell them how cool I am. 



# Feedback

`goo@rust.haus`

[Rust Haus Discord](https://discord.gg/QXhG7KRH)

[Github Discussions](https://github.com/Rust-Haus/RustGPT/discussions)



## License

[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://choosealicense.com/licenses/mit/)

