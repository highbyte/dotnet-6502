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
        public List<string> ConfigurationVariants => new List<string> { "DEFAULT" };

        public IHostSystemConfig GetNewHostSystemConfig()
        {
            return new TestHostSystemConfig();
        }


        public Task<ISystemConfig> GetNewConfig(string configurationVariant)
        {
            return Task.FromResult<ISystemConfig>(new TestSystemConfig());
        }

        public Task PersistConfig(ISystemConfig systemConfig)
        {
            return Task.CompletedTask;
        }

        public ISystem BuildSystem(ISystemConfig systemConfig)
        {
            return new TestSystem();
        }

        public SystemRunner BuildSystemRunner(
            ISystem system,
            ISystemConfig systemConfig,
            IHostSystemConfig hostSystemConfig,
            NullRenderContext renderContext,
            NullInputHandlerContext inputHandlerContext,
            NullAudioHandlerContext audioHandlerContext
            )
        {
            var testSystem = (TestSystem)system;
            var testSystemConfig = (TestSystemConfig)systemConfig;

            var renderer = new NullRenderer(testSystem);
            var inputHandler = new NullInputHandler(testSystem);
            var audioHandler = new NullAudioHandler(testSystem);

            return new SystemRunner(testSystem, renderer, inputHandler, audioHandler);

        }
    }

    public class TestSystem2Configurer : ISystemConfigurer<NullRenderContext, NullInputHandlerContext, NullAudioHandlerContext>
    {
        public string SystemName => TestSystem2.SystemName;
        public List<string> ConfigurationVariants => new List<string> { "DEFAULT" };

        public IHostSystemConfig GetNewHostSystemConfig()
        {
            return new TestHostSystem2Config();
        }

        public Task<ISystemConfig> GetNewConfig(string configurationVariant)
        {
            return Task.FromResult<ISystemConfig>(new TestSystem2Config());
        }

        public Task PersistConfig(ISystemConfig systemConfig)
        {
            return Task.CompletedTask;
        }

        public ISystem BuildSystem(ISystemConfig systemConfig)
        {
            return new TestSystem2();
        }

        public SystemRunner BuildSystemRunner(
            ISystem system,
            ISystemConfig systemConfig,
            IHostSystemConfig hostSystemConfig,
            NullRenderContext renderContext,
            NullInputHandlerContext inputHandlerContext,
            NullAudioHandlerContext audioHandlerContext
            )
        {
            var testSystem2 = (TestSystem2)system;
            var testSystem2Config = (TestSystemConfig)systemConfig;

            var renderer = new NullRenderer(testSystem2);
            var inputHandler = new NullInputHandler(testSystem2);
            var audioHandler = new NullAudioHandler(testSystem2);

            return new SystemRunner(testSystem2, renderer, inputHandler, audioHandler);

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
        public object Clone()
        {
            var clone = (TestHostSystemConfig)MemberwiseClone();
            return clone;
        }
    }

    public class TestHostSystem2Config : IHostSystemConfig
    {
        public object Clone()
        {
            var clone = (TestHostSystem2Config)MemberwiseClone();
            return clone;
        }
    }
}

