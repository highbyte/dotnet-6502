using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Highbyte.DotNet6502.Systems;
using Highbyte.DotNet6502.Systems.Commodore64;
using ModelContextProtocol.Protocol;

namespace Highbyte.DotNet6502.Util.MCPServer;

public static class C64ToolHelper
{
    public static void AssertC64EmulatorIsRunning(IHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        if (hostApp.EmulatorState != EmulatorState.Running)
        {
            throw new InvalidOperationException($"C64 emulator is not running. Current state: {hostApp.EmulatorState}");
        }
    }

    public static void AssertC64EmulatorIsRunningOrPaused(IHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        if (hostApp.EmulatorState != EmulatorState.Running && hostApp.EmulatorState != EmulatorState.Paused)
        {
            throw new InvalidOperationException($"C64 emulator is not running or paused. Current state: {hostApp.EmulatorState}");
        }
    }

    public static void AssertC64EmulatorIsPausedOrUninitialzied(IHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        if (hostApp.EmulatorState != EmulatorState.Paused && hostApp.EmulatorState != EmulatorState.Uninitialized)
        {
            throw new InvalidOperationException($"C64 emulator not paused uninitialzied. Current state: {hostApp.EmulatorState}");
        }
    }

    public static void AssertC64EmulatorIsUninitialzied(IHostApp hostApp)
    {
        AssertEmulatorIsC64(hostApp);
        if (hostApp.EmulatorState != EmulatorState.Uninitialized)
        {
            throw new InvalidOperationException($"C64 emulator is running or paused. Current state: {hostApp.EmulatorState}");
        }
    }

    public static void AssertEmulatorIsC64(IHostApp hostApp)
    {
        if (hostApp.SelectedSystemName != C64.SystemName)
        {
            throw new InvalidOperationException("Current emulated system is not a C64 instance.");
        }
    }

    public static C64 GetC64(IHostApp hostApp)
    {
        if (hostApp.CurrentRunningSystem is C64 c64)
        {
            return c64;
        }
        throw new InvalidOperationException("Current running system is not a C64 instance.");
    }

    public static CallToolResult BuildCallToolDataResult(object data)
    {
        // NOTE: Doesn't seem to work with returning StructuredContent, it's always empty. 
        //JsonNode? jsonNode = JsonSerializer.SerializeToNode(data, s_jsonSerializerOptions);
        //return new CallToolResult
        //{
        //    StructuredContent = jsonNode
        //};
        Type type = data.GetType();
        bool needsJsonElementName;
        if (type.IsEnum || type == typeof(JsonArray))
        {
            needsJsonElementName = true;
        }
        else if (type.IsArray)
        {
            var elementType = type.GetElementType();
            needsJsonElementName = elementType.IsPrimitive || elementType == typeof(string) || elementType == typeof(decimal);
        }
        else
        {
            needsJsonElementName = type.IsPrimitive || type == typeof(string) || type == typeof(decimal);
        }

        object objectToSerialize;
        if (needsJsonElementName)
        {
            objectToSerialize = new
            {
                Data = data
            };
        }
        else
        {
            objectToSerialize = data;
        }
        string json = JsonSerializer.Serialize(objectToSerialize, s_jsonSerializerOptions);
        return new CallToolResult
        {
            Content = new List<ContentBlock>
                {
                    new TextContentBlock
                    {
                        Text = json
                    }
                }
        };
    }

    //public static CallToolResult BuildCallToolDataResult(JsonNode jsonNode)
    //{
    //    // NOTE: Doesn't seem to work with returning StructuredContent, it's always empty. 
    //    //return new CallToolResult
    //    //{
    //    //    StructuredContent = jsonNode
    //    //};
    //}

    public static CallToolResult BuildCallToolErrorResult(Exception ex)
    {
        return new CallToolResult
        {
            IsError = true,
            Content = new List<ContentBlock>
                {
                    new TextContentBlock
                    {
                        Text = $"C64 emulator error: {ex.Message}"
                    },
                },
        };
    }


    private static readonly JsonSerializerOptions s_jsonSerializerOptions = BuildJsonSerializerOptions();

    public static JsonSerializerOptions BuildJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            //WriteIndented = true,
            //DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
