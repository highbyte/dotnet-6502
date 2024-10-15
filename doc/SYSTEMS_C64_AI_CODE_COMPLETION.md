<h1 align="center">Commodore64 Basic AI Code Completion</h1>

# Overview

There is an experimental AI-powered Basic coding assistant available in the [C64 emulator ](SYSTEMS_C64.md) that can be turned on. This a work in progress.

## Background

> [!NOTE] 
> If AI capabilities like GitHub CoPilot were available in the 80s, how could a similar experience be had in the then existing Commodore 64 Basic editor? That was the thought experiment that lead to the idea of trying to integrate a AI coding assistant inside the C64 Basic (with existing 80s UI limitations).

## Use
> [!IMPORTANT]  
> Assuming the coding assistant is [configured](#configure) correctly, the assistant can be used when writing Commodore 64 Basic programs inside the emulator.

### Turn on/off
Use the `AI Basic` checkbox or F9 to toggle assistant on/off.

### Code suggestions
- After a Basic line number has been entered, and then stopped typing for a short while, a suggestion for the rest of the line will be displayed in grey text. 
- Accept the suggestion by pressing `Tab`.
- Ignore the suggestion by pressing any other key.

> [!INFORMATION]
> - Suggestions only appear after you've entered a line number (that is not followed by `REM` comment statement).
> - If no suggestion appears, it's either because the AI backend could not give any suggestion, or that the request to the AI backend failed for some reason (verify that the connection works via Config UI, see [here](#configure)).

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
> - Use this at your own risk. Using your own OpenAI API key will use your own credit grants. It's a good idea to set a limit in OpenAI for how much can be used.
> - You can inspect the network traffic from browser (F12 devtool) to see which (and how many) requests are done to OpenAI.
> - The OpenAI API key is only stored in your browser local storage, it's not stored on any server. This can be verified in the browser with the F12 devtool.

- Set `AI backend type` to `OpenAI`.
- Set `OpenAI API key` to the OpenAI API key. 
- Press `Test` to verify that OpenAI API key works against OpenAI API.

#### AI backend type: `OpenAISelfHostedCodeLlama`
If you host your own AI model with [Ollama](https://ollama.com/), you can use a local `CodeLlama-code` model as source for the C64 Basic AI assistant.

> [!IMPORTANT]  
> - With Ollama installed, download a CodeLlama-code model for example `codellama:13b-code` or `codellama:7b-code`. The larger the model the better, as long as your machine can handle it. Other types of models (non CodeLlama-code) may not work.
>   - Ex. download model: `ollama pull codellama:13b-code`
> - Make sure Ollama (or any proxy in front of it) has configured [CORS Settings](https://medium.com/dcoderai/how-to-handle-cors-settings-in-ollama-a-comprehensive-guide-ee2a5a1beef0) to allow requests from the site running the WebAssembly version of the Emulator (or all *).

- Set `AI backend type` to `OpenAISelfHostedCodeLlama`.
- Set `Self-hosted OpenAI compatible endpoint (Ollama)` to the self-hosted endpoint. The default is `http://localhost:11434/api`
- Set `Model name` to a locally installed CodeLlama-code model, for example `codellama:13b-code` or `codellama:7b-code`. Other non CodeLlama-code models may not work.
- Optionally set `Self-hosted API key (optional)` if a API key is required to access the self-hosted endpoint (for example if Open WebUI is used as a proxy in front of Ollama endpoint).
- Press `Test` to verify that OpenAI API key works against OpenAI API.


#### AI backend type: `None`
If you want to disable the coding assistant.
- Set `AI backend type` to `None`.

![C64 Basic AI code completion](Screenshots/WASM_C64_Basic_AI_Config.png 'C64 Basic AI code completion')

###  SadConsole version
Configure `CodingAssistant` section in `appsettings.json`.

Using OpenAI:
- `CodingAssistantType:OpenAI:CodingAssistantType`: `OpenAI`
- `CodingAssistantType:OpenAI:ApiKey`: Your own OpenAI API key
- `CodingAssistantType:OpenAI:ModelName`: The OpenAI model (default: `gpt-4o`)

Using self-hosted OpenAI API compatible LLM (Ollama with CodeLlama-code model):
- `CodingAssistantType:OpenAISelfHostedCodeLlama:CodingAssistantType`: `OpenAI`
- `CodingAssistantType:OpenAISelfHostedCodeLlama:EndPoint`: The local Ollama HTTP endpoint (ex: `http://localhost:11434/api`)
- `CodingAssistantType:OpenAISelfHostedCodeLlama:ModelName`: A local CodeLlama-code model (ex: `codellama:13b-code` or `codellama:7b-code`.)
- `CodingAssistantType:OpenAISelfHostedCodeLlama:ApiKey`: Optional. May be required if Open WebUI proxy is in front of Ollama.

Using custom AI backend:
TODO

Using no assistant:
- `CodingAssistantType:OpenAI:CodingAssistantType`: `None`

In the emulator UI, use the `C64 Config` -> Basic AI assistant -> `Test` button to verify the connection.
