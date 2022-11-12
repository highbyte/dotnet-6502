namespace Highbyte.DotNet6502.Systems.Commodore64.Models
{
    public static class C64ModelInventory
    {
        public static Dictionary<string, C64ModelBase> C64Models = new();

        static C64ModelInventory()
        {
            C64ModelBase c64Model;
            c64Model = new C64ModelNTSC();
            C64Models.Add(c64Model.Name, c64Model);
            c64Model = new C64ModelPAL();
            C64Models.Add(c64Model.Name, c64Model);
        }
    }
}