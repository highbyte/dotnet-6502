using Highbyte.DotNet6502.Systems.Commodore64.Video;

namespace Highbyte.DotNet6502.Systems.Commodore64
{
    public static class C64Variants
    {
        public static Dictionary<string, Vic2VariantSettingsBase> Vic2Variants = new();

        static C64Variants()
        {
            // VIC2 variants
            Vic2VariantSettingsBase variant;
            variant = new Vic2VariantSettingsNTSC();
            Vic2Variants.Add(variant.Name, variant);
            variant = new Vic2VariantSettingsNTSC_old();
            Vic2Variants.Add(variant.Name, variant);
            variant = new Vic2VariantSettingsPAL();
            Vic2Variants.Add(variant.Name, variant);
        }
    }
}