// using Highbyte.DotNet6502.App.RemoteClient;

// namespace Highbyte.DotNet6502.App.RemoteClient.Tests;

// public class RemoteClientRequestBuilderTests
// {
//     [Fact]
//     public void Build_JoystickSetSupportsExplicitReleaseAliases()
//     {
//         var result = RemoteClientRequestBuilder.Build(["joystick.set", "--port", "1", "--no-up", "--fire"]);

//         Assert.Null(result.Error);
//         Assert.NotNull(result.Request);
//         Assert.Equal(1, Assert.IsType<int>(result.Request!["port"]));
//         Assert.False(Assert.IsType<bool>(result.Request["up"]));
//         Assert.True(Assert.IsType<bool>(result.Request["fire"]));
//     }

//     [Fact]
//     public void Build_JoystickSetSupportsBooleanOptionValues()
//     {
//         var result = RemoteClientRequestBuilder.Build(["joystick.set", "--port", "2", "--left", "false", "--right", "true"]);

//         Assert.Null(result.Error);
//         Assert.NotNull(result.Request);
//         Assert.Equal(2, Assert.IsType<int>(result.Request!["port"]));
//         Assert.False(Assert.IsType<bool>(result.Request["left"]));
//         Assert.True(Assert.IsType<bool>(result.Request["right"]));
//     }

//     [Fact]
//     public void Build_JoystickSetRejectsConflictingFlags()
//     {
//         var result = RemoteClientRequestBuilder.Build(["joystick.set", "--up", "--no-up"]);

//         Assert.Null(result.Request);
//         Assert.Equal("Conflicting options for joystick action 'up': use either --up or --no-up, not both.", result.Error);
//     }

//     [Fact]
//     public void Build_JoystickPressUsesPositiveActionFlags()
//     {
//         var result = RemoteClientRequestBuilder.Build(["joystick.press", "--port", "1", "--up", "--fire"]);

//         Assert.Null(result.Error);
//         Assert.NotNull(result.Request);
//         Assert.Equal("joystick.press", Assert.IsType<string>(result.Request!["cmd"]));
//         Assert.Equal(1, Assert.IsType<int>(result.Request["port"]));
//         Assert.True(Assert.IsType<bool>(result.Request["up"]));
//         Assert.True(Assert.IsType<bool>(result.Request["fire"]));
//     }

//     [Fact]
//     public void Build_JoystickReleaseAllRequiresPort()
//     {
//         var result = RemoteClientRequestBuilder.Build(["joystick.releaseall"]);

//         Assert.Null(result.Request);
//         Assert.Equal("joystick.releaseall requires --port <1|2>.", result.Error);
//     }

//     [Fact]
//     public void Build_JoystickPressRejectsFrameScopedReleaseAlias()
//     {
//         var result = RemoteClientRequestBuilder.Build(["joystick.press", "--no-up"]);

//         Assert.Null(result.Request);
//         Assert.Equal("--no-up is only supported for joystick.set.", result.Error);
//     }
// }