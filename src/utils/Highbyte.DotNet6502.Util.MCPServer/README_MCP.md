# MCP server commands
The MCP server communicates over STDIO with JSON.

To troubleshoot the JSON commands, start the project `Highbyte.DotNet6502.Util.MCPServer.csproj` and paste the JSON commands in the terminal and press enter.

## List tools
```json
{"jsonrpc":"2.0","method":"tools/list","params":{},"id":1}
```


## Examples: state handling

```json
{"jsonrpc":"2.0","method":"tools/call","params":{"name":"GetState","args":{}},"id":2}
```

```json
{"jsonrpc":"2.0","method":"tools/call","params":{"name":"Start","args":{}},"id":2}
```

```json
{"jsonrpc":"2.0","method":"tools/call","params":{"name":"GetCPURegisters","args":{}},"id":2}
```
