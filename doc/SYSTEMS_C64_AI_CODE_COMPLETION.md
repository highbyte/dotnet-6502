<h1 align="center">Commodore64 Basic AI Code Completion</h1>

# Overview

There is an experimental AI-powered Basic coding assistant available in the [C64 emulator ](SYSTEMS_C64.md) that can be turned on. This a work in progress.

## Background

> [!NOTE] 
> If AI capabilities like GitHub CoPilot were available in the 80s, how could a similar experience be had in the existing Commodore 64 Basic editor? That was the thought experiment that lead to the idea of trying to integrate a AI coding assistant inside the C64 Basic (with existing 80s UI limitations).

## Use
> [!IMPORTANT]  
> Assuming the coding assistant is [configured](Configure) correctly, the assistant can be used when writing Commodore 64 Basic programs inside the emulator.

### Turn on/off
Use the checkbox `AI Basic` or F9 to toggle assistant on/off.

### Code suggestions
- After a Basic line number has been entered, and then stopped typing for a short while, a suggestion for the rest of the line will be displayed in grey text. 
- Accept the suggestion by pressing `Tab`.
- Ignore the suggestion by pressing any other key.
- Suggestions only appear after you've entered a line number (that is not followed by `REM` comment statement).

> [!TIP]
> You can enter a Basic line with a comment (`REM` statement) and explain what you want the program to do. Then when you start typing the next several lines it will continuously suggest new code that will build up your program based on the comment.

![C64 Basic AI code completion](Screenshots/WASM_C64_Basic_AI.png 'C64 Basic AI code completion')


## Configure
> [!IMPORTANT]  
> The AI coding assistant is currently available in the `WebAssembly` and `SadConsole` versions of the emulator.

###  WebAssembly version
The configuration is done in the `Configuration` section -> `C64 Config` button -> `Basic AI coding assistant` section.

#### AI backend type: `CustomEndpoint` (temporarily available)
There is a _temporarily available_ AI backend that won't require your own OpenAI API key. This is currently the default setting.
- Select `AI backend type` to `CustomEndpoint`.
- Press `Test` to verify that custom endpoint is available.
- It will be a bit slower than using OpenAI directly.

> [!NOTE]
> The field `Custom endpoint API key` is currently pre-populated with a public available key for the custom endpoint (_it's not an OpenAI key_). This is for future use.

#### AI backend type: `OpenAI`
If you have your own OpenAI API key, you can connect to OpenAI directly. 

> [!CAUTION]
> Use this at your own risk. Using your own OpenAI API key will use your own credit grants. It's a good idea to set a limit in OpenAI for how much can be used.
> You can inspect the network traffic from browser (F12 devtool) to see which (and how many) requests are done to OpenAI.
> The OpenAI API key is only stored in your browser local storage, it's not stored on any server. This can be verified in the browser with the F12 devtool.

- Set `AI backend type` to `OpenAI`.
- Set `OpenAI API key` to the OpenAI API key. 
- Press `Test` to verify that OpenAI API key works against OpenAI API.

#### AI backend type: `LocalOpenAICompatible`
TODO

#### AI backend type: `None`
If you want to disable the coding assistant.
- Set `AI backend type` to `None`.

![C64 Basic AI code completion](Screenshots/WASM_C64_Basic_AI_Config.png 'C64 Basic AI code completion')

###  SadConsole version
Configure `CodingAssistant` section in `appsettings.json`.

Using OpenAI:
- `CodingAssistantType:OpenAI:CodingAssistantType`: `OpenAI`
- `CodingAssistantType:OpenAI:ApiKey`: Your own OpenAI API key
- `CodingAssistantType:OpenAI:DeploymentName`: The OpenAI model (default: `gpt-4o`)

Using self-hosted OpenAI API compatible LLM (such as Ollama):
- `CodingAssistantType:OpenAI:CodingAssistantType`: `OpenAI`
- `CodingAssistantType:OpenAI:SelfHosted`: `true`
- `CodingAssistantType:OpenAI:EndPoint`: The local HTTP endpoint (ex: http://localhost:11434/api)
- `CodingAssistantType:OpenAI:DeploymentName`: The local model name (ex: llama3.1:8b)

Using custom AI backend:
TODO

Using no assistant:
- `CodingAssistantType:OpenAI:CodingAssistantType`: `None`

In the emulator UI, use the `C64 Config` -> Basic AI assistant -> `Test` button to verify the connection.
