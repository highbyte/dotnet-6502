using Microsoft.Extensions.Logging.Abstractions;
using static Highbyte.DotNet6502.Systems.Tests.SystemConfigurerTests;

namespace Highbyte.DotNet6502.Systems.Tests;

public class HostAppTests
{
    [Fact]
    public void InitializingWithoutSettingContextsThrowsException()
    {
        // Arrange
        var testApp = BuildTestHostApp(setContexts: false, initContexts: false);

        // Act / Assert
        var ex = Assert.Throws<DotNet6502Exception>(() => testApp.InitRenderContext());
        Assert.Contains($"RenderContext has not been set", ex.Message);

        ex = Assert.Throws<DotNet6502Exception>(() => testApp.InitInputHandlerContext());
        Assert.Contains($"InputHandlerContext has not been set", ex.Message);

        ex = Assert.Throws<DotNet6502Exception>(() => testApp.InitAudioHandlerContext());
        Assert.Contains($"AudioHandlerContext has not been set", ex.Message);
    }

    [Fact]
    public void InitializingWithContextsWorks()
    {
        // Arrange
        var testApp = BuildTestHostApp(setContexts: true, initContexts: false);

        // Act / Assert
        testApp.InitRenderContext();
        testApp.InitInputHandlerContext();
        testApp.InitAudioHandlerContext();
    }

    [Fact]
    public void SelectingSystemThatDoesntExistThrowsException()
    {
        // Arrange
        var testApp = BuildTestHostApp();

        // Act / Assert
        var ex = Assert.Throws<DotNet6502Exception>(() => testApp.SelectSystem("SystemThatDoesNotExist"));
        Assert.Contains($"System not found", ex.Message);
    }

    [Fact]
    public async Task SelectingSystemWhileRunningThrowsException()
    {
        // Arrange
        var testApp = BuildTestHostApp();
        testApp.SelectSystem(testApp.AvailableSystemNames.First());
        await testApp.Start();

        // Act / Assert
        var ex = Assert.Throws<DotNet6502Exception>(() => testApp.SelectSystem(testApp.AvailableSystemNames.Last()));
        Assert.Contains($"Cannot change system while emulator is running", ex.Message);
    }

    [Fact]
    public async Task StartingWhileAlreadyRunningThrowsException()
    {
        // Arrange
        var testApp = BuildTestHostApp();
        testApp.SelectSystem(testApp.AvailableSystemNames.First());
        await testApp.Start();

        // Act / Assert
        var ex = await Assert.ThrowsAsync<DotNet6502Exception>(async () => await testApp.Start());
        Assert.Contains($"Cannot start emulator if emulator is running", ex.Message);
    }

    [Fact]
    public async Task StartingIfSystemConfigIsInvalidThrowsException()
    {
        // Arrange
        var testApp = BuildTestHostApp();
        testApp.SelectSystem(TestSystem.SystemName);
        var systemConfig = (TestSystemConfig)await testApp.GetSystemConfigClone();
        systemConfig.TestIsValid = false;
        testApp.UpdateSystemConfig(systemConfig);

        // Act / Assert
        var ex = await Assert.ThrowsAsync<DotNet6502Exception>(async () => await testApp.Start());
        Assert.Contains($"Cannot start emulator if current system config is invalid", ex.Message);
    }

    [Fact]
    public async Task StartingCallsOnBeforeStart()
    {
        // Arrange
        var testApp = BuildTestHostApp();
        testApp.SelectSystem(testApp.AvailableSystemNames.First());

        // Act
        await testApp.Start();

        // Assert
    }

    public class TestHostApp : HostApp<NullRenderContext, NullInputHandlerContext, NullAudioHandlerContext>
    {
        public TestHostApp(
            SystemList<NullRenderContext, NullInputHandlerContext, NullAudioHandlerContext> systemList,
            Dictionary<string, IHostSystemConfig> hostSystemConfigs
            ) : base("TestHost", systemList, hostSystemConfigs, new NullLoggerFactory())
        {
        }
    }

    private TestHostApp BuildTestHostApp(bool setContexts = true, bool initContexts = true)
    {
        var systemList = new SystemList<NullRenderContext, NullInputHandlerContext, NullAudioHandlerContext>();

        var systemConfigurer = new TestSystemConfigurer();
        systemList.AddSystem(systemConfigurer);

        var system2Configurer = new TestSystem2Configurer();
        systemList.AddSystem(system2Configurer);

        var testApp = new TestHostApp(systemList, new Dictionary<string, IHostSystemConfig>());

        if (setContexts)
        {
            var testRenderContext = new NullRenderContext();
            var testInputHandlerContext = new NullInputHandlerContext();
            var testAudioHandlerContext = new NullAudioHandlerContext();

            testApp.SetContexts(() => testRenderContext, () => testInputHandlerContext, () => testAudioHandlerContext);
        }
        if (initContexts)
        {
            testApp.InitRenderContext();
            testApp.InitInputHandlerContext();
            testApp.InitAudioHandlerContext();
        }
        return testApp;
    }
}
