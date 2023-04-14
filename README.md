# Rust GPT Plugin 🤖

  

Are you sick of playing Rust like a normal person? Yeah, us too. That's why we created the Rust GPT plugin, so you can chat with an AI while you wander aimlessly through the map, waiting to get shot out of nowhere. Who needs a game plan when you have an AI to talk to?

  


## Features 🔥


- Ask GPT questions directly from the game chat
- Get text-based answers from GPT
- Customizable cooldown time
- Participate in the inevitable AI overlord takeover!

## Contributing

Contributions are always welcome! Reach out to Goo on Rust Haus' [discord](https://discord.gg/EQNPBxdjRu) if you want. Otherwise do a pull request and get crackin.


## Plugin Variables

To run this project, you will need to get yourself a fancy [OpenAI API key](https://platform.openai.com/account/api-keys).



## Installation 🛠️


Copy the plugin `RustGPT.cs` into your Carbon or Oxide plugins folder.

When the plugin is first loaded it will generate a configuration file and the server console will say `Please configure the plugin with the required OpenAI API key.`  

Configure the plugin in the `RustGPT.json` file located in you Oxide or Carbon `configs` folder. 

As of ver 1.5 the config file looks like this:

```json
{
"CooldownTimeInSeconds": 30,
"GptAssistantIntro": "You are an assistant on a Rust game server.",
"OpenAIApiKey": "<YOUR API KEY>",
"PluginVersion": "1.5.0"
}
```    

To quickly get movin and groovin just paste in your OpenAI API key and restart the plugin.

Hop in to the console and restart the plugin. 

Using Oxide? Do this `o.reload RustGPT.cs`

Using Carbon? Do this `c.reload RustGPT.cs`

Do yourself a favor and make a local copy of your `RustGPT.json` file in case something goes wonky in future updates. 



## Usage/Examples

You can do this to have the AI answer some questions with a little more accuracy. 

```json
"GptAssistantIntro": "You are an assistant on the Rust game server called Rust Haus. The Rust Haus server wipes every Thursday around 2pm CST or if forced. Discord server at https://discord.gg/EQNPBxdjRu, website is https://rust.haus, Twitter is @HausRust81322.",
```

While in game chat the AI will trigger when a question is asked in chat that ends in a qestion mark. 

Currently the question structure is like this: 
`who|what|when|where|why|how|is|are|am|do|does|did|can|could|will|would|should|which|whom "rest of the question" ?`

For convenience there is the chat command `/askgpt` Where you can input content directly to the AI without worrying about asking a question. Don't try and train the AI this way it won't work...yet.

## Authors

- [@purnellbp](https://www.github.com/purnellbp)

I'm not associated in any way with OpenAI or Facepunch but feel free to tell them how cool I am. 


## Feedback

If you have any feedback, please reach out to `goo@rust.haus`



![Logo](https://i.imgur.com/KttasYy.png)

[![MIT License](https://img.shields.io/badge/License-MIT-green.svg)](https://choosealicense.com/licenses/mit/)

