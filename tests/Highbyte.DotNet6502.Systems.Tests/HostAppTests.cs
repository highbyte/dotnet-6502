using Highbyte.DotNet6502.Systems.Audio;
using Highbyte.DotNet6502.Systems.Input;
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
        var ex = Assert.Throws<DotNet6502Exception>(() => testApp.InitInputHandlerContext());
        Assert.Contains($"InputHandlerContext has not been set", ex.Message);
    }

    [Fact]
    public void InitializingWithContextsWorks()
    {
        // Arrange
        var testApp = BuildTestHostApp(setContexts: true, initContexts: false);

        // Act / Assert
        testApp.InitInputHandlerContext();
    }

    [Fact]
    public async Task SelectingSystemThatDoesntExistThrowsException()
    {
        // Arrange
        var testApp = BuildTestHostApp();

        // Act / Assert
        var ex = await Assert.ThrowsAsync<DotNet6502Exception>(async () => await testApp.SelectSystem("SystemThatDoesNotExist"));
        Assert.Contains($"System not found", ex.Message);
    }

    [Fact]
    public async Task SelectingSystemWhileRunningThrowsException()
    {
        // Arrange
        var testApp = BuildTestHostApp();
        await testApp.SelectSystem(testApp.AvailableSystemNames.First());
        await testApp.Start();

        // Act / Assert
        var ex = await Assert.ThrowsAsync<DotNet6502Exception>(async () => await testApp.SelectSystem(testApp.AvailableSystemNames.Last()));
        Assert.Contains($"Cannot change system while emulator is running", ex.Message);
    }

    [Fact]
    public async Task StartingWhileAlreadyRunningThrowsException()
    {
        // Arrange
        var testApp = BuildTestHostApp();
        await testApp.SelectSystem(testApp.AvailableSystemNames.First());
        await testApp.Start();

        // Act / Assert
        var ex = await Assert.ThrowsAsync<DotNet6502Exception>(async () => await testApp.Start());
        Assert.Contains($"Cannot start emulator if emulator is running", ex.Message);
    }

    [Fact]
    public async Task StartingIfHostSystemConfigIsInvalidThrowsException()
    {
        // Arrange
        var testApp = BuildTestHostApp();
        await testApp.SelectSystem(TestSystem.SystemName);
        var hostSystemConfig = (TestHostSystemConfig)testApp.CurrentHostSystemConfig;
        hostSystemConfig.TestIsValid = false;

        // Act / Assert
        var ex = await Assert.ThrowsAsync<DotNet6502Exception>(async () => await testApp.Start());
        Assert.Contains($"Cannot start emulator, system config is invalid", ex.Message);
    }

    [Fact]
    public async Task StartingCallsOnBeforeStart()
    {
        // Arrange
        var testApp = BuildTestHostApp();
        await testApp.SelectSystem(testApp.AvailableSystemNames.First());

        // Act
        await testApp.Start();

        // Assert
    }

    [Fact]
    public async Task UpdateHostSystemConfig_WhenEmulatorIsUninitialized_RebuildsTemporarySystem()
    {
        // Arrange: start then stop the emulator so _selectedSystemTemporary is cleared
        var testApp = BuildTestHostApp();
        await testApp.SelectSystem(TestSystem.SystemName);
        await testApp.Start();
        testApp.Stop(); // Stop() sets _selectedSystemTemporary = null

        // Pre-condition: GetSelectedSystem() should be null after Stop()
        var systemBeforeUpdate = await testApp.GetSelectedSystem();
        Assert.Null(systemBeforeUpdate);

        // Act: update config (simulates uploading ROMs after first load)
        var newValidConfig = new TestHostSystemConfig(); // TestIsValid defaults to true
        testApp.UpdateHostSystemConfig(newValidConfig);

        // Assert: GetSelectedSystem() should now return the rebuilt system
        var systemAfterUpdate = await testApp.GetSelectedSystem();
        Assert.NotNull(systemAfterUpdate);
    }

    [Fact]
    public async Task UpdateHostSystemConfig_WhenConfigBecomesValid_RebuildsTemporarySystem()
    {
        // Arrange: select system then force the config to become invalid (simulates first run without ROMs)
        var testApp = BuildTestHostApp();
        await testApp.SelectSystem(TestSystem.SystemName);

        var invalidConfig = new TestHostSystemConfig { TestIsValid = false };
        testApp.UpdateHostSystemConfig(invalidConfig); // config is now invalid -> _selectedSystemTemporary = null

        // Pre-condition: GetSelectedSystem() returns null when config is invalid
        var systemBeforeFixup = await testApp.GetSelectedSystem();
        Assert.Null(systemBeforeFixup);

        // Act: update with valid config (simulates ROMs being uploaded via config dialog)
        var validConfig = new TestHostSystemConfig { TestIsValid = true };
        testApp.UpdateHostSystemConfig(validConfig);

        // Assert: GetSelectedSystem() should return the rebuilt system
        var systemAfterFixup = await testApp.GetSelectedSystem();
        Assert.NotNull(systemAfterFixup);
    }

    [Fact]
    public async Task CurrentSystemScreenInfo_WhenSelectedSystemCannotBeBuilt_UsesConfigurerScreenInfo()
    {
        // Arrange: select a system, then make config invalid so no temporary system can be built.
        var testApp = BuildTestHostApp();
        await testApp.SelectSystem(TestSystem.SystemName);
        testApp.UpdateHostSystemConfig(new TestHostSystemConfig { TestIsValid = false });

        // Act
        var screenInfo = testApp.CurrentSystemScreenInfo;

        // Assert
        Assert.NotNull(screenInfo);
        Assert.Equal(123, screenInfo.VisibleWidth);
        Assert.Equal(67, screenInfo.VisibleHeight);
    }

    [Fact]
    public async Task SelectSystem_WhenPreviousSystemIsInvalid_UpdatesVariantBeforeSystemChangedNotification()
    {
        // Arrange: the first system is selected but cannot be built, so screen info must come from
        // the selected system/variant rather than from a temporary system instance.
        var testApp = BuildTestHostApp();
        await testApp.SelectSystem(TestSystem.SystemName);
        testApp.UpdateHostSystemConfig(new TestHostSystemConfig { TestIsValid = false });
        testApp.ReadScreenInfoOnSelectedSystemChanged = true;

        // Act: switch to a system whose GetScreenInfo rejects stale variants.
        await testApp.SelectSystem(TestSystem2.SystemName);

        // Assert: reading screen info during the selected-system notification saw the new variant.
        Assert.Equal(TestSystem2Configurer.Variant, testApp.SelectedSystemConfigurationVariant);
        Assert.NotNull(testApp.ScreenInfoReadDuringSelectedSystemChanged);
        Assert.Equal(246, testApp.ScreenInfoReadDuringSelectedSystemChanged.VisibleWidth);
        Assert.Equal(134, testApp.ScreenInfoReadDuringSelectedSystemChanged.VisibleHeight);
    }

    public class TestHostApp : HostApp
    {
        public TestHostApp(
            SystemList systemList
            ) : base("TestHost", systemList, new NullLoggerFactory())
        {
        }

        public bool ReadScreenInfoOnSelectedSystemChanged { get; set; }
        public IScreen? ScreenInfoReadDuringSelectedSystemChanged { get; private set; }

        public override void OnAfterSelectedSystemChanged()
        {
            if (ReadScreenInfoOnSelectedSystemChanged)
                ScreenInfoReadDuringSelectedSystemChanged = CurrentSystemScreenInfo;

            base.OnAfterSelectedSystemChanged();
        }
    }

    private TestHostApp BuildTestHostApp(bool setContexts = true, bool initContexts = true)
    {
        var systemList = new SystemList();

        var systemConfigurer = new TestSystemConfigurer();
        systemList.AddSystem(systemConfigurer);

        var system2Configurer = new TestSystem2Configurer();
        systemList.AddSystem(system2Configurer);

        var testApp = new TestHostApp(systemList);

        if (setContexts)
        {
            var testInputHandlerContext = new NullInputHandlerContext();
            testApp.SetContexts(() => testInputHandlerContext);
        }
        if (initContexts)
        {
            testApp.InitInputHandlerContext();
        }
        return testApp;
    }
}
