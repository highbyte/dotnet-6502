using Highbyte.DotNet6502.Systems.Commodore64.Config;

namespace Highbyte.DotNet6502.Systems.Tests;

public class SystemConfigurerTests
{
    [Fact]
    public void TODO()
    {
        // Arrange

        // Act

        // Assert
    }


    public class TestSystemConfigurer : ISystemConfigurer<NullRenderContext, NullInputHandlerContext, NullAudioHandlerContext>
    {
        public string SystemName => TestSystem.SystemName;
        public Task<List<string>> GetConfigurationVariants(IHostSystemConfig hostSystemConfig) => Task.FromResult(new List<string> { "DEFAULT" });

        public Task<IHostSystemConfig> GetNewHostSystemConfig()
        {
            return Task.FromResult<IHostSystemConfig>(new TestHostSystemConfig());
        }

        public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
        {
            return Task.CompletedTask;
        }

        public Task<ISystem> BuildSystem(string configurationVariant, IHostSystemConfig hostSystemConfig)
        {
            return Task.FromResult<ISystem>(new TestSystem());
        }

        public Task<SystemRunner> BuildSystemRunner(
            ISystem system,
            IHostSystemConfig hostSystemConfig,
            NullRenderContext renderContext,
            NullInputHandlerContext inputHandlerContext,
            NullAudioHandlerContext audioHandlerContext
            )
        {
            var testSystem = (TestSystem)system;

            var renderer = new NullRenderer(testSystem);
            var inputHandler = new NullInputHandler(testSystem);
            var audioHandler = new NullAudioHandler(testSystem);

            return Task.FromResult(new SystemRunner(testSystem, renderer, inputHandler, audioHandler));

        }
    }

    public class TestSystem2Configurer : ISystemConfigurer<NullRenderContext, NullInputHandlerContext, NullAudioHandlerContext>
    {
        public string SystemName => TestSystem2.SystemName;
        public Task<List<string>> GetConfigurationVariants(IHostSystemConfig hostSystemConfig) => Task.FromResult(new List<string> { "DEFAULT" });

        public Task<IHostSystemConfig> GetNewHostSystemConfig()
        {
            return Task.FromResult<IHostSystemConfig>(new TestHostSystem2Config());
        }

        public Task PersistHostSystemConfig(IHostSystemConfig hostSystemConfig)
        {
            return Task.CompletedTask;
        }

        public Task<ISystem> BuildSystem(string configurationVariant, IHostSystemConfig hostSystemConfig)
        {
            return Task.FromResult<ISystem>(new TestSystem2());
        }

        public Task<SystemRunner> BuildSystemRunner(
            ISystem system,
            IHostSystemConfig hostSystemConfig,
            NullRenderContext renderContext,
            NullInputHandlerContext inputHandlerContext,
            NullAudioHandlerContext audioHandlerContext
            )
        {
            var testSystem2 = (TestSystem2)system;

            var renderer = new NullRenderer(testSystem2);
            var inputHandler = new NullInputHandler(testSystem2);
            var audioHandler = new NullAudioHandler(testSystem2);

            return Task.FromResult(new SystemRunner(testSystem2, renderer, inputHandler, audioHandler));
        }
    }

    public class TestSystemConfig : ISystemConfig
    {
        public bool TestIsValid = true;
        public List<string> TestValidationErrors = new List<string>();

        public bool AudioSupported { get => false; set => _ = value; }
        public bool AudioEnabled { get => false; set => _ = value; }

        public object Clone()
        {
            var clone = (TestSystemConfig)MemberwiseClone();
            return clone;
        }

        public bool IsValid(out List<string> validationErrors)
        {
            validationErrors = TestValidationErrors;
            return TestIsValid;
        }

        public void Validate()
        {
        }
    }

    public class TestSystem2Config : ISystemConfig
    {
        public bool TestIsValid = true;
        public List<string> TestValidationErrors = new List<string>();

        public bool AudioSupported { get => false; set => _ = value; }
        public bool AudioEnabled { get => false; set => _ = value; }

        public object Clone()
        {
            throw new NotImplementedException();
        }

        public bool IsValid(out List<string> validationErrors)
        {
            validationErrors = TestValidationErrors;
            return TestIsValid;
        }

        public void Validate()
        {
        }
    }

    public class TestHostSystemConfig : IHostSystemConfig
    {
        public bool TestIsValid = true;

        private TestSystemConfig _systemConfig;
        ISystemConfig IHostSystemConfig.SystemConfig => _systemConfig;

        public TestSystemConfig SystemConfig => _systemConfig;

        public bool AudioSupported => false;

        public TestHostSystemConfig()
        {
            _systemConfig = new TestSystemConfig();
        }

        public object Clone()
        {
            var clone = (TestHostSystemConfig)MemberwiseClone();
            return clone;
        }

        public void Validate()
        {
        }

        public bool IsValid(out List<string> validationErrors)
        {
            validationErrors = new();
            return TestIsValid;
        }
    }

    public class TestHostSystem2Config : IHostSystemConfig
    {
        private TestSystem2Config _systemConfig;
        ISystemConfig IHostSystemConfig.SystemConfig => _systemConfig;

        public TestSystem2Config SystemConfig => _systemConfig;

        public bool AudioSupported => false;

        public TestHostSystem2Config()
        {
            _systemConfig = new TestSystem2Config();
        }

        public object Clone()
        {
            var clone = (TestHostSystem2Config)MemberwiseClone();
            clone._systemConfig = (TestSystem2Config)_systemConfig.Clone();
            return clone;
        }

        public void Validate()
        {
        }

        public bool IsValid(out List<string> validationErrors)
        {
            validationErrors = new();
            return validationErrors.Count == 0;
        }
    }
}
